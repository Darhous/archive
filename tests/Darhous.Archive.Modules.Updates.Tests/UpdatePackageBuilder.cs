using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace Darhous.Archive.Modules.Updates.Tests;

internal static class UpdatePackageBuilder
{
    public static X509Certificate2 CreateCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Darhous Update Test Publisher",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1));
    }

    public static string Build(
        string directory,
        X509Certificate2 certificate,
        string version = "2.0.0",
        bool includesMigration = false,
        bool corruptPayload = false,
        bool omitSignature = false,
        IReadOnlyDictionary<string, string>? files = null)
    {
        files ??= new Dictionary<string, string> { ["app.txt"] = "new-version" };
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            componentType = UpdateModuleOptions.ApplicationComponentType,
            componentId = UpdateModuleOptions.ApplicationComponentId,
            version,
            includesMigration,
        });

        var payloads = files.ToDictionary(
            pair => "payload/" + pair.Key.Replace('\\', '/'),
            pair => Encoding.UTF8.GetBytes(pair.Value),
            StringComparer.Ordinal);
        var checksums = payloads.ToDictionary(
            pair => pair.Key,
            pair => Convert.ToHexStringLower(SHA256.HashData(pair.Value)),
            StringComparer.Ordinal);
        checksums["manifest.json"] = Convert.ToHexStringLower(SHA256.HashData(manifest));
        var checksumBytes = JsonSerializer.SerializeToUtf8Bytes(checksums);

        byte[]? signature = null;
        if (!omitSignature)
        {
            var cms = new SignedCms(new ContentInfo(checksumBytes), detached: true);
            cms.ComputeSignature(new CmsSigner(certificate));
            signature = cms.Encode();
        }

        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"Darhous-{version}.darhousupdate");
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        Write(archive, "manifest.json", manifest);
        Write(archive, "checksums.json", checksumBytes);
        if (signature is not null)
        {
            Write(archive, "signature.p7s", signature);
        }

        foreach (var (entryName, content) in payloads)
        {
            Write(archive, entryName, corruptPayload ? Encoding.UTF8.GetBytes("tampered") : content);
        }

        return path;
    }

    private static void Write(ZipArchive archive, string name, byte[] content)
    {
        var entry = archive.CreateEntry(name);
        using var output = entry.Open();
        output.Write(content);
    }
}
