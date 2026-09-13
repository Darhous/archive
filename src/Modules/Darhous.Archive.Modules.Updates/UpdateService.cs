using System.Security.Cryptography;
using System.Text.Json;
using Darhous.Archive.Modules.Updates.Packaging;
using Darhous.Archive.Modules.Updates.Persistence;
using Darhous.Backup.Local;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Modules.Updates;

internal sealed class UpdateService(
    IUpdateSource updateSource,
    UpdatePackageValidator packageValidator,
    UpdateHistoryStore history,
    ILocalBackupService backupService,
    UpdateModuleOptions options,
    ILogger<UpdateService> logger) : IUpdateService
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly SemaphoreSlim _checkGate = new(1, 1);

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        await _checkGate.WaitAsync(cancellationToken);
        try
        {
            var installed = await ReadInstalledVersionAsync(cancellationToken);
            var latest = await updateSource.GetLatestAsync(
                options.ComponentType, options.ComponentId, cancellationToken);
            return new UpdateCheckResult(
                options.ComponentType,
                options.ComponentId,
                installed,
                latest,
                latest is not null && latest.Version > installed);
        }
        finally
        {
            _checkGate.Release();
        }
    }

    public async Task<UpdateInstallResult> InstallAsync(
        string packagePath,
        Guid? initiatedBy,
        Func<int, CancellationToken, Task>? reportProgress,
        CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken);
        long? historyId = null;
        string? stagingPath = null;
        try
        {
            // Safety sequence step 1: signature plus complete checksum/manifest validation.
            var validated = await packageValidator.ValidateAsync(packagePath, cancellationToken);
            ValidateTarget(validated);
            var fromVersion = await ReadInstalledVersionAsync(cancellationToken);
            if (validated.Version <= fromVersion)
            {
                throw new InvalidOperationException(
                    $"Update {validated.Version} is not newer than installed version {fromVersion}.");
            }

            historyId = await history.StartAsync(validated.Manifest, fromVersion, initiatedBy, cancellationToken);
            await ReportAsync(reportProgress, 15, cancellationToken);

            string? safetyBackupPath = null;
            if (validated.Manifest.IncludesMigration)
            {
                // Safety sequence step 2. Phase 18 re-opens and validates the completed package
                // before returning, so failure here is a hard blocker before staging/apply.
                var backup = await backupService.CreateBackupAsync(
                    new BackupRequest(BackupType.Metadata, options.SafetyBackupDirectory, initiatedBy),
                    reportProgress: null,
                    cancellationToken);
                safetyBackupPath = backup.PackagePath;
            }

            await ReportAsync(reportProgress, 40, cancellationToken);

            // Safety sequence step 3: stage the verified payload outside the install tree.
            stagingPath = Path.Combine(options.StagingDirectory, $"{historyId}-{validated.Version}");
            EnsureFreshDirectory(stagingPath);
            await packageValidator.StageAsync(
                Path.GetFullPath(packagePath), validated, stagingPath, cancellationToken);
            await ReportAsync(reportProgress, 60, cancellationToken);

            // Safety sequence step 4: snapshot every path that the package will touch.
            var rollbackPoint = await CreateRollbackPointAsync(
                historyId.Value, validated, fromVersion, safetyBackupPath, stagingPath, cancellationToken);
            await ReportAsync(reportProgress, 75, cancellationToken);

            try
            {
                await ApplyStagedFilesAsync(stagingPath, validated.PayloadFiles, cancellationToken);
                await WriteInstalledVersionAsync(validated.Version, cancellationToken);
            }
            catch
            {
                await RestoreFilesAsync(rollbackPoint, cancellationToken);
                await WriteInstalledVersionAsync(fromVersion, cancellationToken);
                throw;
            }

            await history.CompleteAsync(historyId.Value, fromVersion, cancellationToken);
            await ReportAsync(reportProgress, 100, cancellationToken);
            logger.LogWarning(
                "Applied update {FromVersion} -> {ToVersion}; restart is required and locked running binaries may require the Phase 24 external updater",
                fromVersion, validated.Version);
            return new UpdateInstallResult(
                historyId.Value, fromVersion, validated.Version,
                RollbackPointPath(historyId.Value), safetyBackupPath, RequiresRestart: true);
        }
        catch (Exception exception)
        {
            if (historyId.HasValue)
            {
                await RecordFailureBestEffortAsync(historyId.Value, exception);
            }

            throw;
        }
        finally
        {
            if (stagingPath is not null)
            {
                TryDeleteDirectory(stagingPath);
            }

            _operationGate.Release();
        }
    }

    public async Task RollbackAsync(
        long historyId,
        Guid? initiatedBy,
        Func<int, CancellationToken, Task>? reportProgress,
        CancellationToken cancellationToken)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var entry = await history.GetAsync(historyId, cancellationToken)
                ?? throw new InvalidOperationException($"Update history {historyId} was not found.");
            if (entry.Status != "completed" || entry.RollbackVersion is null)
            {
                throw new InvalidOperationException($"Update history {historyId} is not rollback-ready.");
            }

            var rollbackPoint = await ReadRollbackPointAsync(historyId, cancellationToken);
            await RestoreFilesAsync(rollbackPoint, cancellationToken);
            await WriteInstalledVersionAsync(Version.Parse(rollbackPoint.PreviousVersion), cancellationToken);
            await ReportAsync(reportProgress, 60, cancellationToken);

            if (rollbackPoint.SafetyBackupPath is not null)
            {
                await backupService.RestoreAsync(rollbackPoint.SafetyBackupPath, initiatedBy, cancellationToken);
            }

            await history.RolledBackAsync(historyId, cancellationToken);
            await ReportAsync(reportProgress, 100, cancellationToken);
        }
        catch (Exception exception)
        {
            await RecordRollbackFailureBestEffortAsync(historyId, exception);
            throw;
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private void ValidateTarget(ValidatedUpdatePackage package)
    {
        if (!string.Equals(package.Manifest.ComponentType, options.ComponentType, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(package.Manifest.ComponentId, options.ComponentId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Update package targets a different component.");
        }
    }

    private async Task<UpdateRollbackManifest> CreateRollbackPointAsync(
        long historyId,
        ValidatedUpdatePackage package,
        Version previousVersion,
        string? safetyBackupPath,
        string stagingPath,
        CancellationToken cancellationToken)
    {
        var rollbackPath = RollbackPointPath(historyId);
        EnsureFreshDirectory(rollbackPath);
        var files = new List<UpdateRollbackFile>();
        foreach (var relativePath in package.PayloadFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = UpdatePackageValidator.ResolveChild(stagingPath, relativePath);
            var installed = UpdatePackageValidator.ResolveChild(options.InstallDirectory, relativePath);
            if (!File.Exists(installed))
            {
                files.Add(new UpdateRollbackFile(relativePath, HadOriginal: false, Sha256: null));
                continue;
            }

            var snapshot = UpdatePackageValidator.ResolveChild(Path.Combine(rollbackPath, "files"), relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(snapshot)!);
            File.Copy(installed, snapshot, overwrite: false);
            files.Add(new UpdateRollbackFile(
                relativePath,
                HadOriginal: true,
                await HashFileAsync(snapshot, cancellationToken)));
        }

        var manifest = new UpdateRollbackManifest(
            historyId, package.Manifest.ComponentType, package.Manifest.ComponentId,
            previousVersion.ToString(), package.Version.ToString(), safetyBackupPath, files);
        var manifestPath = Path.Combine(rollbackPath, "rollback.json");
        await using var output = new FileStream(
            manifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await JsonSerializer.SerializeAsync(output, manifest, UpdateJsonContext.Default.UpdateRollbackManifest, cancellationToken);
        return manifest;
    }

    private async Task ApplyStagedFilesAsync(
        string stagingPath,
        IReadOnlyList<string> relativePaths,
        CancellationToken cancellationToken)
    {
        foreach (var relativePath in relativePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = UpdatePackageValidator.ResolveChild(stagingPath, relativePath);
            var destination = UpdatePackageValidator.ResolveChild(options.InstallDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            var temporary = destination + $".update-{Guid.NewGuid():N}.tmp";
            try
            {
                File.Copy(source, temporary, overwrite: false);
                File.Move(temporary, destination, overwrite: true);
            }
            finally
            {
                TryDeleteFile(temporary);
            }
        }

        await Task.CompletedTask;
    }

    private async Task RestoreFilesAsync(UpdateRollbackManifest manifest, CancellationToken cancellationToken)
    {
        var rollbackPath = RollbackPointPath(manifest.HistoryId);
        foreach (var file in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destination = UpdatePackageValidator.ResolveChild(options.InstallDirectory, file.RelativePath);
            if (!file.HadOriginal)
            {
                TryDeleteFile(destination);
                continue;
            }

            var snapshot = UpdatePackageValidator.ResolveChild(Path.Combine(rollbackPath, "files"), file.RelativePath);
            var actualHash = await HashFileAsync(snapshot, cancellationToken);
            if (!string.Equals(actualHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Rollback snapshot '{file.RelativePath}' failed checksum validation.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(snapshot, destination, overwrite: true);
        }
    }

    private async Task<UpdateRollbackManifest> ReadRollbackPointAsync(long historyId, CancellationToken cancellationToken)
    {
        var path = Path.Combine(RollbackPointPath(historyId), "rollback.json");
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var manifest = await JsonSerializer.DeserializeAsync(
            stream, UpdateJsonContext.Default.UpdateRollbackManifest, cancellationToken)
            ?? throw new InvalidDataException("Rollback manifest is empty.");
        if (manifest.HistoryId != historyId ||
            !string.Equals(manifest.ComponentType, options.ComponentType, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(manifest.ComponentId, options.ComponentId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Rollback manifest does not match the requested update.");
        }

        return manifest;
    }

    private async Task<Version> ReadInstalledVersionAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(options.ActiveVersionFilePath))
        {
            return options.InstalledVersion;
        }

        var value = await File.ReadAllTextAsync(options.ActiveVersionFilePath, cancellationToken);
        return Version.TryParse(value, out var version)
            ? version
            : throw new InvalidDataException("The active update version marker is invalid.");
    }

    private async Task WriteInstalledVersionAsync(Version version, CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(options.ActiveVersionFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, version.ToString(), cancellationToken);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            TryDeleteFile(temporary);
        }
    }

    private string RollbackPointPath(long historyId) => Path.Combine(options.RollbackDirectory, historyId.ToString());

    private static void EnsureFreshDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            throw new IOException($"Update working directory already exists: '{path}'.");
        }

        Directory.CreateDirectory(path);
    }

    private async Task RecordFailureBestEffortAsync(long historyId, Exception exception)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await history.FailAsync(historyId, exception, timeout.Token);
        }
        catch (Exception recordException)
        {
            logger.LogError(recordException, "Could not mark update {HistoryId} as failed", historyId);
        }
    }

    private async Task RecordRollbackFailureBestEffortAsync(long historyId, Exception exception)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await history.RollbackFailedAsync(historyId, exception, timeout.Token);
        }
        catch (Exception recordException)
        {
            logger.LogError(recordException, "Could not mark rollback {HistoryId} as failed", historyId);
        }
    }

    private static Task ReportAsync(
        Func<int, CancellationToken, Task>? reporter,
        int percent,
        CancellationToken cancellationToken) => reporter?.Invoke(percent, cancellationToken) ?? Task.CompletedTask;

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
