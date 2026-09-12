using System.Security.Cryptography;
using System.Text.Json;

namespace Darhous.Archive.Modules.Plugins.Packaging;

/// <summary>Plugin SDK §38 ("Validate checksums") — <c>checksums.json</c> is a flat map of package-relative path to lowercase hex SHA-256, covering every payload file (not the metadata files themselves).</summary>
public static class PluginChecksumVerifier
{
    public static IReadOnlyList<string> Verify(PluginPackage package)
    {
        var errors = new List<string>();
        var json = package.RawChecksumsJson;
        if (json is null)
        {
            return ["Package is missing checksums.json."];
        }

        Dictionary<string, string>? checksums;
        try
        {
            checksums = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch (JsonException ex)
        {
            return [$"checksums.json is not valid JSON: {ex.Message}"];
        }

        if (checksums is null || checksums.Count == 0)
        {
            return ["checksums.json is empty."];
        }

        foreach (var (entryName, expectedHex) in checksums)
        {
            byte[] bytes;
            try
            {
                bytes = package.ReadEntryBytesRequired(entryName);
            }
            catch (InvalidDataException)
            {
                errors.Add($"checksums.json references '{entryName}', which is not present in the package.");
                continue;
            }

            var actualHex = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (!string.Equals(actualHex, expectedHex, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Checksum mismatch for '{entryName}' — the package may be corrupt or tampered with.");
            }
        }

        return errors;
    }
}
