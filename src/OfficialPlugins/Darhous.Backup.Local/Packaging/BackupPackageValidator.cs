using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Darhous.Backup.Local.Packaging;

internal sealed record ValidatedBackupPackage(string PackagePath, BackupPackageManifest Manifest);

internal sealed class BackupPackageValidator
{
    private static readonly string[] RequiredDatabases =
        ["databases/archive.db", "databases/audit.db", "databases/search.db"];

    public async Task<ValidatedBackupPackage> ValidateAsync(string packagePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        var fullPath = Path.GetFullPath(packagePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Backup package was not found.", fullPath);
        }

        using var archive = ZipFile.OpenRead(fullPath);
        var entries = archive.Entries.ToDictionary(
            entry => NormalizeEntryPath(entry.FullName),
            StringComparer.Ordinal);

        if (!entries.TryGetValue("manifest.json", out var manifestEntry))
        {
            throw new InvalidDataException("Backup package has no manifest.json.");
        }

        BackupPackageManifest manifest;
        await using (var manifestStream = manifestEntry.Open())
        {
            manifest = await JsonSerializer.DeserializeAsync(
                manifestStream, BackupJsonContext.Default.BackupPackageManifest, cancellationToken)
                ?? throw new InvalidDataException("Backup manifest is empty or invalid.");
        }

        ValidateManifestShape(manifest);

        var declaredPaths = manifest.Entries.Select(entry => NormalizeEntryPath(entry.Path)).ToHashSet(StringComparer.Ordinal);
        var actualPaths = entries.Keys.Where(path => path != "manifest.json" && !path.EndsWith("/", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        if (!actualPaths.SetEquals(declaredPaths))
        {
            throw new InvalidDataException("Backup manifest entries do not exactly match package contents.");
        }

        foreach (var expected in manifest.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedPath = NormalizeEntryPath(expected.Path);
            var actual = entries[normalizedPath];
            if (actual.Length != expected.Size)
            {
                throw new InvalidDataException($"Size mismatch for '{normalizedPath}'.");
            }

            await using var stream = actual.Open();
            var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(hash), Convert.FromHexString(expected.Sha256)))
            {
                throw new InvalidDataException($"Checksum mismatch for '{normalizedPath}'.");
            }
        }

        return new ValidatedBackupPackage(fullPath, manifest);
    }

    public async Task ExtractAsync(
        ValidatedBackupPackage package,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDirectory);
        var destinationRoot = Path.GetFullPath(destinationDirectory) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(package.PackagePath);

        foreach (var declared in package.Manifest.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = NormalizeEntryPath(declared.Path);
            var outputPath = Path.GetFullPath(Path.Combine(destinationRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
            if (!outputPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Package entry escapes the extraction root: '{normalized}'.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            var entry = archive.GetEntry(normalized)
                ?? throw new InvalidDataException($"Package entry is missing: '{normalized}'.");
            await using var source = entry.Open();
            await using var destination = new FileStream(
                outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
            await source.CopyToAsync(destination, cancellationToken);
            await destination.FlushAsync(cancellationToken);
        }
    }

    internal static string NormalizeEntryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\\', StringComparison.Ordinal) || Path.IsPathRooted(path))
        {
            throw new InvalidDataException($"Invalid package entry path '{path}'.");
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or ".." || segment.Contains(':', StringComparison.Ordinal)))
        {
            throw new InvalidDataException($"Invalid package entry path '{path}'.");
        }

        return string.Join('/', segments) + (path.EndsWith("/", StringComparison.Ordinal) ? "/" : string.Empty);
    }

    private static void ValidateManifestShape(BackupPackageManifest manifest)
    {
        if (manifest.FormatVersion != 1)
        {
            throw new InvalidDataException($"Unsupported backup format version {manifest.FormatVersion}.");
        }

        _ = BackupTypeNames.Parse(manifest.BackupType);
        if (!Version.TryParse(manifest.ApplicationVersion, out _))
        {
            throw new InvalidDataException("Backup manifest has an invalid application version.");
        }

        foreach (var database in new[] { "archive", "audit", "search" })
        {
            if (!manifest.SchemaVersions.ContainsKey(database))
            {
                throw new InvalidDataException($"Backup manifest has no schema version for {database}.db.");
            }
        }

        var paths = manifest.Entries.Select(entry => NormalizeEntryPath(entry.Path)).ToArray();
        if (paths.Distinct(StringComparer.Ordinal).Count() != paths.Length)
        {
            throw new InvalidDataException("Backup manifest contains duplicate paths.");
        }


        if (paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Length)
        {
            throw new InvalidDataException("Backup manifest contains paths that collide on a case-insensitive filesystem.");
        }

        if (manifest.Entries.Any(entry => entry.Size < 0 || entry.Sha256.Length != 64 || !IsHex(entry.Sha256)))
        {
            throw new InvalidDataException("Backup manifest contains an invalid size or SHA-256 value.");
        }

        if (RequiredDatabases.Any(required => !paths.Contains(required, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("Backup package does not contain all three SQLite databases.");
        }

        var type = BackupTypeNames.Parse(manifest.BackupType);
        if (type == BackupType.Metadata && paths.Any(path => path.StartsWith("files/", StringComparison.Ordinal) || path.StartsWith("settings/", StringComparison.Ordinal)))
        {
            throw new InvalidDataException("A metadata backup cannot contain managed files or settings.");
        }

        if (type == BackupType.Configuration && paths.Any(path => path.StartsWith("files/", StringComparison.Ordinal)))
        {
            throw new InvalidDataException("A configuration backup cannot contain managed document files.");
        }

        if (paths.Any(path => !path.StartsWith("databases/", StringComparison.Ordinal) &&
                              !path.StartsWith("files/", StringComparison.Ordinal) &&
                              !path.StartsWith("settings/", StringComparison.Ordinal)))
        {
            throw new InvalidDataException("Backup package contains an unsupported payload category.");
        }
    }

    private static bool IsHex(string value) => value.All(character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
}
