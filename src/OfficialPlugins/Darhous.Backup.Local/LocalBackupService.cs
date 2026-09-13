using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Migrations;
using Darhous.Archive.Persistence.Writes;
using Darhous.Backup.Local.Packaging;
using Darhous.Search.SqliteFts.Rebuild;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Darhous.Backup.Local;

internal sealed class LocalBackupService(
    LocalBackupOptions options,
    PersistenceOptions persistenceOptions,
    ISqliteConnectionFactory connectionFactory,
    BackupHistoryStore history,
    BackupPackageValidator validator,
    ISearchIndexRebuilder searchIndexRebuilder,
    ISqliteWriteQueue archiveWriteQueue,
    ISqliteWriteQueue auditWriteQueue,
    ISqliteWriteQueue searchWriteQueue,
    IClock clock,
    ILogger<LocalBackupService> logger) : ILocalBackupService
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    public async Task<BackupResult> CreateBackupAsync(
        BackupRequest request,
        Func<int, CancellationToken, Task>? reportProgress,
        CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            return await CreateBackupCoreAsync(request, reportProgress, cancellationToken);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<RestoreResult> RestoreAsync(
        string packagePath,
        Guid? requestedBy,
        CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            return await RestoreCoreAsync(packagePath, requestedBy, cancellationToken);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<BackupResult> CreateBackupCoreAsync(
        BackupRequest request,
        Func<int, CancellationToken, Task>? reportProgress,
        CancellationToken cancellationToken)
    {
        ValidateDestination(request);
        var backupUid = Guid.NewGuid();
        await history.StartAsync(backupUid, request, cancellationToken);

        var stagingRoot = Path.Combine(options.WorkingDirectory, $"backup-{backupUid:N}");
        var partialPath = Path.Combine(request.DestinationDirectory, $".{backupUid:N}.partial");
        try
        {
            Directory.CreateDirectory(stagingRoot);
            await ReportAsync(reportProgress, 5, cancellationToken);

            var databaseDirectory = Path.Combine(stagingRoot, "databases");
            Directory.CreateDirectory(databaseDirectory);
            foreach (var database in Enum.GetValues<DatabaseKind>())
            {
                await BackupDatabaseAsync(database, Path.Combine(databaseDirectory, DatabaseFileName(database)), cancellationToken);
            }

            await ReportAsync(reportProgress, 35, cancellationToken);

            if (request.Type == BackupType.Full)
            {
                await FileTree.CopyAsync(options.ManagedFilesDirectory, Path.Combine(stagingRoot, "files"), cancellationToken);
            }

            if (request.Type is BackupType.Full or BackupType.Configuration)
            {
                await FileTree.CopyAsync(options.SettingsDirectory, Path.Combine(stagingRoot, "settings"), cancellationToken);
            }

            await ReportAsync(reportProgress, 65, cancellationToken);

            var entries = new List<BackupPackageEntry>();
            foreach (var (file, relativePath) in FileTree.EnumerateFiles(stagingRoot))
            {
                var packagePath = relativePath.Replace(Path.DirectorySeparatorChar, '/');
                entries.Add(new BackupPackageEntry(
                    packagePath,
                    new FileInfo(file).Length,
                    await FileTree.Sha256Async(file, cancellationToken)));
            }

            entries.Sort((left, right) => StringComparer.Ordinal.Compare(left.Path, right.Path));
            var schemaVersions = await ReadSchemaVersionsAsync(databaseDirectory, cancellationToken);
            var manifest = new BackupPackageManifest(
                1, backupUid, request.Type.ToStorageName(), options.ApplicationVersion.ToString(),
                clock.UtcNow, schemaVersions, entries);

            await CreatePackageAsync(stagingRoot, partialPath, manifest, cancellationToken);
            await ReportAsync(reportProgress, 85, cancellationToken);

            var finalPath = Path.Combine(
                Path.GetFullPath(request.DestinationDirectory),
                $"Darhous-{request.Type.ToStorageName()}-{clock.UtcNow:yyyyMMdd-HHmmss}-{backupUid:N}.darhousbackup");
            File.Move(partialPath, finalPath);

            await validator.ValidateAsync(finalPath, cancellationToken);
            var result = new BackupResult(
                backupUid, request.Type, finalPath, new FileInfo(finalPath).Length,
                await FileTree.Sha256Async(finalPath, cancellationToken));
            await history.CompleteAsync(backupUid, result, cancellationToken);
            await ReportAsync(reportProgress, 100, cancellationToken);
            logger.LogInformation("Created {BackupType} backup {BackupUid} at {PackagePath}", request.Type, backupUid, finalPath);
            return result;
        }
        catch (Exception exception)
        {
            await RecordFailureBestEffortAsync(backupUid, exception);
            throw;
        }
        finally
        {
            TryDeleteFile(partialPath);
            TryDeleteDirectory(stagingRoot);
        }
    }

    private async Task<RestoreResult> RestoreCoreAsync(
        string packagePath,
        Guid? requestedBy,
        CancellationToken cancellationToken)
    {
        var package = await validator.ValidateAsync(packagePath, cancellationToken);
        ValidateRestoreVersion(package.Manifest);

        var restoreUid = Guid.NewGuid();
        var extractionRoot = Path.Combine(options.WorkingDirectory, $"restore-{restoreUid:N}");
        var safetyExtractionRoot = Path.Combine(options.WorkingDirectory, $"restore-safety-{restoreUid:N}");
        var preparedSwaps = new List<DirectorySwap>();
        var leases = new Dictionary<DatabaseKind, ISqliteMaintenanceLease>();
        var searchLeaseReleased = false;
        var criticalRestoreCompleted = false;
        string? safetyBackupPath = null;

        try
        {
            Directory.CreateDirectory(extractionRoot);
            await validator.ExtractAsync(package, extractionRoot, cancellationToken);
            await VerifyExtractedDatabasesAsync(extractionRoot, cancellationToken);
            await RejectNewerSchemaAsync(package.Manifest, cancellationToken);

            var safety = await CreateBackupCoreAsync(
                new BackupRequest(BackupType.Full, options.SafetyBackupDirectory, requestedBy),
                reportProgress: null,
                cancellationToken);
            safetyBackupPath = safety.PackagePath;
            var safetyPackage = await validator.ValidateAsync(safety.PackagePath, cancellationToken);
            Directory.CreateDirectory(safetyExtractionRoot);
            await validator.ExtractAsync(safetyPackage, safetyExtractionRoot, cancellationToken);

            var type = BackupTypeNames.Parse(package.Manifest.BackupType);
            if (type == BackupType.Full)
            {
                preparedSwaps.Add(await DirectorySwap.PrepareAsync(
                    Path.Combine(extractionRoot, "files"), options.ManagedFilesDirectory, cancellationToken));
            }

            if (type is BackupType.Full or BackupType.Configuration)
            {
                preparedSwaps.Add(await DirectorySwap.PrepareAsync(
                    Path.Combine(extractionRoot, "settings"), options.SettingsDirectory, cancellationToken));
            }

            try
            {
                leases[DatabaseKind.Archive] = await archiveWriteQueue.PauseAsync(cancellationToken);
                leases[DatabaseKind.Audit] = await auditWriteQueue.PauseAsync(cancellationToken);
                leases[DatabaseKind.Search] = await searchWriteQueue.PauseAsync(cancellationToken);
                SqliteConnection.ClearAllPools();

                foreach (var database in Enum.GetValues<DatabaseKind>())
                {
                    await RestoreDatabaseAsync(
                        leases[database], Path.Combine(extractionRoot, "databases", DatabaseFileName(database)), cancellationToken);
                }

                foreach (var swap in preparedSwaps)
                {
                    swap.Apply();
                }

                foreach (var database in Enum.GetValues<DatabaseKind>())
                {
                    MigrationRunnerFactory.MigrateUp(database, persistenceOptions);
                }

                criticalRestoreCompleted = true;
            }
            catch (Exception restoreException)
            {
                if (leases.Count == 3)
                {
                    try
                    {
                        foreach (var database in Enum.GetValues<DatabaseKind>())
                        {
                            await RestoreDatabaseAsync(
                                leases[database], Path.Combine(safetyExtractionRoot, "databases", DatabaseFileName(database)), CancellationToken.None);
                        }

                        foreach (var swap in preparedSwaps.AsEnumerable().Reverse())
                        {
                            swap.Rollback();
                        }
                    }
                    catch (Exception rollbackException)
                    {
                        var aggregate = new AggregateException(
                            "Restore failed and automatic rollback to the safety backup also failed.",
                            restoreException, rollbackException);
                        throw new RestoreRecoveryRequiredException(
                            aggregate.Message, safety.PackagePath, aggregate);
                    }
                }

                throw;
            }

            await leases[DatabaseKind.Search].DisposeAsync();
            leases.Remove(DatabaseKind.Search);
            searchLeaseReleased = true;
            await searchIndexRebuilder.RebuildAsync(cancellationToken);

            await VerifyLiveStateAsync(package.Manifest, preparedSwaps, leases, cancellationToken);
            foreach (var swap in preparedSwaps)
            {
                swap.Complete();
            }

            logger.LogWarning(
                "Restored backup {BackupUid}. Safety backup retained at {SafetyBackupPath}",
                package.Manifest.BackupUid, safety.PackagePath);
            return new RestoreResult(package.Manifest.BackupUid, type, safety.PackagePath, RequiresRestart: true);
        }
        catch (Exception exception) when (criticalRestoreCompleted && searchLeaseReleased &&
                                          exception is not RestoreRecoveryRequiredException)
        {
            logger.LogCritical(
                "Restore replaced live databases but failed during search rebuild or final verification. Use the retained safety backup before continuing.");
            throw new RestoreRecoveryRequiredException(
                "Restore changed the live state but search rebuild or final verification failed. Stop the application and recover from the safety backup.",
                safetyBackupPath ?? string.Empty,
                exception);
        }
        finally
        {
            foreach (var lease in leases.Values.Reverse())
            {
                await lease.DisposeAsync();
            }

            foreach (var swap in preparedSwaps)
            {
                swap.DisposeUnapplied();
            }

            TryDeleteDirectory(extractionRoot);
            TryDeleteDirectory(safetyExtractionRoot);
        }
    }

    private void ValidateDestination(BackupRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DestinationDirectory);
        var destination = Path.GetFullPath(request.DestinationDirectory);
        if (!Directory.Exists(destination))
        {
            throw new DirectoryNotFoundException($"Backup destination does not exist: '{destination}'.");
        }

        var sources = new List<string> { persistenceOptions.DataDirectory };
        if (request.Type == BackupType.Full)
        {
            sources.Add(options.ManagedFilesDirectory);
        }

        if (request.Type is BackupType.Full or BackupType.Configuration)
        {
            sources.Add(options.SettingsDirectory);
        }

        if (sources.Any(source => IsSameOrChild(destination, source)))
        {
            throw new InvalidOperationException("Backup destination cannot be inside a directory being backed up.");
        }

        if (sources.Any(source => IsSameOrChild(options.WorkingDirectory, source)))
        {
            throw new InvalidOperationException("Backup working directory cannot be inside a directory being backed up.");
        }

        Directory.CreateDirectory(options.WorkingDirectory);
        var probe = Path.Combine(destination, $".darhous-write-probe-{Guid.NewGuid():N}");
        try
        {
            using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.WriteThrough);
            stream.WriteByte(0);
            stream.Flush(flushToDisk: true);
        }
        finally
        {
            TryDeleteFile(probe);
        }
    }

    private async Task BackupDatabaseAsync(DatabaseKind database, string destinationPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var source = await connectionFactory.OpenAsync(database, cancellationToken);
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = destinationPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        };
        await using var destination = new SqliteConnection(builder.ToString());
        await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
        await VerifyDatabaseAsync(destinationPath, cancellationToken);
    }

    private static async Task RestoreDatabaseAsync(
        ISqliteMaintenanceLease lease,
        string sourcePath,
        CancellationToken cancellationToken)
    {
        await lease.ExecuteAsync(async (destination, ct) =>
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = sourcePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            };
            await using var source = new SqliteConnection(builder.ToString());
            await source.OpenAsync(ct);
            source.BackupDatabase(destination);
            return true;
        }, cancellationToken);
    }

    private static async Task CreatePackageAsync(
        string stagingRoot,
        string partialPath,
        BackupPackageManifest manifest,
        CancellationToken cancellationToken)
    {
        await using var output = new FileStream(
            partialPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.WriteThrough);
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        foreach (var item in manifest.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = archive.CreateEntry(item.Path, CompressionLevel.Optimal);
            await using var source = new FileStream(
                Path.Combine(stagingRoot, item.Path.Replace('/', Path.DirectorySeparatorChar)),
                FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var destination = entry.Open();
            await source.CopyToAsync(destination, cancellationToken);
        }

        var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
        await using var manifestStream = manifestEntry.Open();
        await JsonSerializer.SerializeAsync(
            manifestStream, manifest, BackupJsonContext.Default.BackupPackageManifest, cancellationToken);
    }

    private static async Task<IReadOnlyDictionary<string, long>> ReadSchemaVersionsAsync(
        string databaseDirectory,
        CancellationToken cancellationToken)
    {
        var versions = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var database in Enum.GetValues<DatabaseKind>())
        {
            versions[database.ToString().ToLowerInvariant()] = await ReadSchemaVersionAsync(
                Path.Combine(databaseDirectory, DatabaseFileName(database)), cancellationToken);
        }

        return versions;
    }

    private async Task RejectNewerSchemaAsync(BackupPackageManifest manifest, CancellationToken cancellationToken)
    {
        foreach (var database in Enum.GetValues<DatabaseKind>())
        {
            var current = await ReadLiveSchemaVersionAsync(database, cancellationToken);
            var backup = manifest.SchemaVersions[database.ToString().ToLowerInvariant()];
            if (backup > current)
            {
                throw new InvalidDataException(
                    $"Backup {database} schema {backup} is newer than the current application schema {current}.");
            }
        }
    }

    private async Task<long> ReadLiveSchemaVersionAsync(DatabaseKind database, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(database, cancellationToken);
        return await ReadSchemaVersionAsync(connection, cancellationToken);
    }

    private static async Task<long> ReadSchemaVersionAsync(string databasePath, CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);
        return await ReadSchemaVersionAsync(connection, cancellationToken);
    }

    private static async Task<long> ReadSchemaVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_migrations;";
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static async Task VerifyDatabaseAsync(string databasePath, CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = databasePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);
        await VerifyDatabaseAsync(connection, cancellationToken);
    }

    private static async Task VerifyDatabaseAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = (string?)(await command.ExecuteScalarAsync(cancellationToken));
        if (!string.Equals(result, "ok", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"SQLite integrity_check failed: {result ?? "no result"}.");
        }
    }

    private static async Task VerifyExtractedDatabasesAsync(string extractionRoot, CancellationToken cancellationToken)
    {
        foreach (var database in Enum.GetValues<DatabaseKind>())
        {
            await VerifyDatabaseAsync(Path.Combine(extractionRoot, "databases", DatabaseFileName(database)), cancellationToken);
        }
    }

    private async Task VerifyLiveStateAsync(
        BackupPackageManifest manifest,
        IReadOnlyList<DirectorySwap> swaps,
        IReadOnlyDictionary<DatabaseKind, ISqliteMaintenanceLease> activeLeases,
        CancellationToken cancellationToken)
    {
        foreach (var database in new[] { DatabaseKind.Archive, DatabaseKind.Audit })
        {
            await activeLeases[database].ExecuteAsync(async (connection, ct) =>
            {
                await VerifyDatabaseAsync(connection, ct);
                return true;
            }, cancellationToken);
        }

        await using (var search = await connectionFactory.OpenAsync(DatabaseKind.Search, cancellationToken))
        {
            await VerifyDatabaseAsync(search, cancellationToken);
        }

        foreach (var swap in swaps)
        {
            var prefix = string.Equals(swap.TargetPath, Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.ManagedFilesDirectory)), StringComparison.OrdinalIgnoreCase)
                ? "files/"
                : "settings/";
            var expectedEntries = manifest.Entries
                .Where(entry => entry.Path.StartsWith(prefix, StringComparison.Ordinal))
                .ToDictionary(entry => entry.Path, StringComparer.Ordinal);
            var actualEntries = FileTree.EnumerateFiles(swap.TargetPath)
                .ToDictionary(
                    item => prefix + item.RelativePath.Replace(Path.DirectorySeparatorChar, '/'),
                    item => item.FullPath,
                    StringComparer.Ordinal);
            if (!actualEntries.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expectedEntries.Keys))
            {
                throw new InvalidDataException($"Restored {prefix.TrimEnd('/')} file set does not match the manifest.");
            }

            foreach (var (manifestPath, path) in actualEntries)
            {
                var expected = expectedEntries[manifestPath];
                if (new FileInfo(path).Length != expected.Size ||
                    !string.Equals(await FileTree.Sha256Async(path, cancellationToken), expected.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Restored file verification failed for '{manifestPath}'.");
                }
            }
        }
    }

    private void ValidateRestoreVersion(BackupPackageManifest manifest)
    {
        var backupVersion = Version.Parse(manifest.ApplicationVersion);
        if (backupVersion > options.ApplicationVersion)
        {
            throw new InvalidDataException(
                $"Backup was created by newer application version {backupVersion}; current version is {options.ApplicationVersion}.");
        }
    }

    private async Task RecordFailureBestEffortAsync(Guid backupUid, Exception exception)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await history.FailAsync(backupUid, exception, timeout.Token);
        }
        catch (Exception historyException)
        {
            logger.LogError(historyException, "Could not mark backup {BackupUid} as failed", backupUid);
        }
    }

    private static Task ReportAsync(
        Func<int, CancellationToken, Task>? reporter,
        int percent,
        CancellationToken cancellationToken) =>
        reporter?.Invoke(percent, cancellationToken) ?? Task.CompletedTask;

    private static string DatabaseFileName(DatabaseKind database) => database switch
    {
        DatabaseKind.Archive => "archive.db",
        DatabaseKind.Audit => "audit.db",
        DatabaseKind.Search => "search.db",
        _ => throw new ArgumentOutOfRangeException(nameof(database)),
    };

    private static bool IsSameOrChild(string candidate, string parent)
    {
        var candidatePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        var parentPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
        return candidatePath.Equals(parentPath, StringComparison.OrdinalIgnoreCase) ||
               candidatePath.StartsWith(parentPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private sealed class DirectorySwap(string targetPath, string stagedPath, string previousPath)
    {
        private bool _applied;
        private bool _completed;

        public string TargetPath { get; } = targetPath;

        public static async Task<DirectorySwap> PrepareAsync(
            string sourcePath,
            string targetPath,
            CancellationToken cancellationToken)
        {
            var target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetPath));
            var parent = Path.GetDirectoryName(target)
                ?? throw new InvalidOperationException($"Restore target has no parent: '{target}'.");
            Directory.CreateDirectory(parent);
            var staged = Path.Combine(parent, $".darhous-restore-{Guid.NewGuid():N}");
            var previous = Path.Combine(parent, $".darhous-previous-{Guid.NewGuid():N}");
            Directory.CreateDirectory(staged);
            await FileTree.CopyAsync(sourcePath, staged, cancellationToken);
            return new DirectorySwap(target, staged, previous);
        }

        public void Apply()
        {
            if (Directory.Exists(TargetPath))
            {
                Directory.Move(TargetPath, previousPath);
            }

            try
            {
                Directory.Move(stagedPath, TargetPath);
                _applied = true;
            }
            catch
            {
                if (Directory.Exists(previousPath) && !Directory.Exists(TargetPath))
                {
                    Directory.Move(previousPath, TargetPath);
                }

                throw;
            }
        }

        public void Rollback()
        {
            if (!_applied) return;
            if (Directory.Exists(TargetPath)) Directory.Delete(TargetPath, recursive: true);
            if (Directory.Exists(previousPath)) Directory.Move(previousPath, TargetPath);
            _applied = false;
        }

        public void Complete()
        {
            TryDeleteDirectory(previousPath);
            _completed = true;
        }

        public void DisposeUnapplied()
        {
            if (!_applied && Directory.Exists(stagedPath)) TryDeleteDirectory(stagedPath);
            if (_completed) return;
        }
    }
}
