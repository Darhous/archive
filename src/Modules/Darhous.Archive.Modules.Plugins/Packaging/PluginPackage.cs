using System.IO.Compression;

namespace Darhous.Archive.Modules.Plugins.Packaging;

/// <summary>
/// An opened <c>.archiveplugin</c> (Plugin SDK §8 — a ZIP file). Kept open for the caller's
/// use (checksum verification needs to read every entry's bytes) and must be disposed when
/// done — <see cref="PluginPackageValidator"/> owns that lifetime.
/// </summary>
public sealed class PluginPackage(ZipArchive archive) : IDisposable
{
    public string RawManifestJson => ReadEntryText("manifest.json") ?? throw new InvalidDataException("Package is missing manifest.json.");

    public string? RawChecksumsJson => ReadEntryText("checksums.json");

    public byte[]? SignatureBytes => ReadEntryBytes("signature.p7s");

    public IEnumerable<string> EntryNames => archive.Entries.Select(e => e.FullName);

    public byte[] ReadEntryBytesRequired(string entryName) =>
        ReadEntryBytes(entryName) ?? throw new InvalidDataException($"Package is missing entry '{entryName}'.");

    private byte[]? ReadEntryBytes(string entryName)
    {
        var entry = archive.GetEntry(entryName);
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private string? ReadEntryText(string entryName)
    {
        var bytes = ReadEntryBytes(entryName);
        return bytes is null ? null : System.Text.Encoding.UTF8.GetString(bytes);
    }

    public void ExtractTo(string destinationDirectory)
    {
        foreach (var entry in archive.Entries)
        {
            // Skip the package's own metadata files — only the plugin's actual payload
            // (bin/, resources/, migrations/) gets installed.
            if (entry.FullName is "manifest.json" or "checksums.json" or "signature.p7s")
            {
                continue;
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                continue; // directory entry
            }

            var destinationPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName));
            if (!destinationPath.StartsWith(Path.GetFullPath(destinationDirectory), StringComparison.Ordinal))
            {
                // Zip-slip guard — a malicious "../../.." entry name must never write outside staging.
                throw new InvalidDataException($"Package entry '{entry.FullName}' resolves outside the install directory.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }

    public void Dispose() => archive.Dispose();

    public static PluginPackage Open(string packagePath) => new(ZipFile.OpenRead(packagePath));
}
