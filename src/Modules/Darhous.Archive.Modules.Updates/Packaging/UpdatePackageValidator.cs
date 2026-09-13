using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Darhous.Archive.Modules.Plugins.Packaging;

namespace Darhous.Archive.Modules.Updates.Packaging;

internal sealed class UpdatePackageValidator(
    IPluginTrustStore trustStore,
    UpdateModuleOptions options)
{
    private static readonly HashSet<string> MetadataEntries =
        ["manifest.json", "checksums.json", "signature.p7s"];

    public async Task<ValidatedUpdatePackage> ValidateAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        var fullPath = Path.GetFullPath(packagePath);
        var file = new FileInfo(fullPath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("Update package was not found.", fullPath);
        }

        if (file.Length > options.MaximumPackageBytes)
        {
            throw new InvalidDataException("Update package exceeds the configured size limit.");
        }

        await using var stream = new FileStream(
            fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var packageHash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        ValidateEntrySet(archive);

        var manifestBytes = await ReadRequiredAsync(archive, "manifest.json", 1024 * 1024, cancellationToken);
        var checksumBytes = await ReadRequiredAsync(archive, "checksums.json", 16 * 1024 * 1024, cancellationToken);
        var signatureBytes = await ReadOptionalAsync(archive, "signature.p7s", 1024 * 1024, cancellationToken);

        var signatureErrors = CmsPackageSignatureVerifier.Verify(checksumBytes, signatureBytes, trustStore);
        if (signatureErrors.Count > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, signatureErrors));
        }

        Dictionary<string, string> checksums;
        try
        {
            checksums = JsonSerializer.Deserialize(checksumBytes, UpdateJsonContext.Default.DictionaryStringString)
                ?? throw new InvalidDataException("checksums.json is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("checksums.json is invalid.", exception);
        }

        var payloadEntries = archive.Entries
            .Where(entry => !string.IsNullOrEmpty(entry.Name) && !MetadataEntries.Contains(entry.FullName))
            .ToArray();
        var expectedNames = payloadEntries.Select(entry => entry.FullName)
            .Append("manifest.json")
            .ToHashSet(StringComparer.Ordinal);
        if (!expectedNames.SetEquals(checksums.Keys))
        {
            throw new InvalidDataException("checksums.json must cover manifest.json and every payload file exactly once.");
        }

        long extractedBytes = 0;
        foreach (var entryName in expectedNames)
        {
            var entry = archive.GetEntry(entryName)!;
            extractedBytes = checked(extractedBytes + entry.Length);
            if (extractedBytes > options.MaximumExtractedBytes)
            {
                throw new InvalidDataException("Update package exceeds the configured extracted-size limit.");
            }

            var actual = await HashEntryAsync(entry, cancellationToken);
            if (!string.Equals(actual, checksums[entryName], StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Checksum mismatch for '{entryName}'.");
            }
        }

        UpdatePackageManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize(manifestBytes, UpdateJsonContext.Default.UpdatePackageManifest)
                ?? throw new InvalidDataException("manifest.json is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("manifest.json is invalid.", exception);
        }

        if (manifest.SchemaVersion != 1 || string.IsNullOrWhiteSpace(manifest.ComponentType) ||
            string.IsNullOrWhiteSpace(manifest.ComponentId) ||
            !Version.TryParse(manifest.Version, out var version))
        {
            throw new InvalidDataException("Update package manifest has invalid required fields.");
        }

        return new ValidatedUpdatePackage(
            manifest,
            version,
            payloadEntries.Select(entry => entry.FullName["payload/".Length..]).ToArray(),
            packageHash);
    }

    public async Task StageAsync(
        string packagePath,
        ValidatedUpdatePackage validated,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDirectory);
        var stagedPackagePath = Path.Combine(destinationDirectory, "package.darhousupdate");
        await using (var stream = new FileStream(
            packagePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
        await using (var stagedPackage = new FileStream(
            stagedPackagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await stream.CopyToAsync(stagedPackage, cancellationToken);
        }

        // The source can be user-selected and live on removable media. Pin the exact bytes
        // verified before backup, then re-run signature/checksum validation on the private
        // staged copy before extracting it.
        var stagedValidation = await ValidateAsync(stagedPackagePath, cancellationToken);
        if (!string.Equals(stagedValidation.PackageSha256, validated.PackageSha256, StringComparison.OrdinalIgnoreCase) ||
            stagedValidation.Manifest != validated.Manifest ||
            !stagedValidation.PayloadFiles.SequenceEqual(validated.PayloadFiles, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Update package changed after signature verification.");
        }

        await using var verifiedStream = new FileStream(
            stagedPackagePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var archive = new ZipArchive(verifiedStream, ZipArchiveMode.Read, leaveOpen: false);
        foreach (var relativePath in validated.PayloadFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entryName = "payload/" + relativePath.Replace(Path.DirectorySeparatorChar, '/');
            var entry = archive.GetEntry(entryName)
                ?? throw new InvalidDataException($"Verified package entry '{entryName}' disappeared before staging.");
            var destination = ResolveChild(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var source = entry.Open();
            await using var output = new FileStream(
                destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.WriteThrough);
            await source.CopyToAsync(output, cancellationToken);
        }
    }

    internal static string ResolveChild(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException($"Package path '{relativePath}' is rooted.");
        }

        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var candidate = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));
        if (!candidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Package path '{relativePath}' escapes its target directory.");
        }

        return candidate;
    }

    private static void ValidateEntrySet(ZipArchive archive)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            if (!names.Add(entry.FullName))
            {
                throw new InvalidDataException($"Update package contains duplicate entry '{entry.FullName}'.");
            }

            if (!string.IsNullOrEmpty(entry.Name) &&
                !MetadataEntries.Contains(entry.FullName) &&
                !entry.FullName.StartsWith("payload/", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Unexpected update package entry '{entry.FullName}'.");
            }

            if (entry.FullName.StartsWith("payload/", StringComparison.Ordinal) && !string.IsNullOrEmpty(entry.Name))
            {
                _ = ResolveChild("C:\\package-root", entry.FullName["payload/".Length..]);
            }
        }
    }

    private static async Task<byte[]> ReadRequiredAsync(
        ZipArchive archive,
        string entryName,
        long maximumBytes,
        CancellationToken cancellationToken) =>
        await ReadOptionalAsync(archive, entryName, maximumBytes, cancellationToken)
            ?? throw new InvalidDataException($"Update package is missing '{entryName}'.");

    private static async Task<byte[]?> ReadOptionalAsync(
        ZipArchive archive,
        string entryName,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry(entryName);
        if (entry is null)
        {
            return null;
        }

        if (entry.Length > maximumBytes)
        {
            throw new InvalidDataException($"Update package entry '{entryName}' exceeds its size limit.");
        }

        await using var source = entry.Open();
        using var destination = new MemoryStream();
        await source.CopyToAsync(destination, cancellationToken);
        return destination.ToArray();
    }

    private static async Task<string> HashEntryAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using var stream = entry.Open();
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }
}
