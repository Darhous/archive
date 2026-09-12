using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace Darhous.Archive.Modules.Plugins.Tests;

/// <summary>Builds a genuinely valid (or deliberately broken, for negative tests) <c>.archiveplugin</c> ZIP package at test time — no binary fixture checked into the repo.</summary>
internal static class TestPackageBuilder
{
    public static X509Certificate2 CreateSigningCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Darhous Test Publisher", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(1));
    }

    public sealed record BuildOptions
    {
        public string PluginId { get; init; } = "Darhous.Test.SamplePlugin";
        public string Version { get; init; } = "1.0.0";
        public string MinCoreVersion { get; init; } = "1.0.0";
        public IReadOnlyList<string> Permissions { get; init; } = [];
        public IReadOnlyList<object> Dependencies { get; init; } = [];
        public bool CorruptChecksum { get; init; }
        public bool OmitSignature { get; init; }
        public X509Certificate2? SigningCertificate { get; init; }
    }

    public static string Build(string outputDirectory, BuildOptions options)
    {
        var testPluginDllPath = typeof(Darhous.TestPlugin.TestPlugin).Assembly.Location;
        var dllBytes = File.ReadAllBytes(testPluginDllPath);

        var manifest = new Dictionary<string, object?>
        {
            ["schemaVersion"] = "1.0",
            ["id"] = options.PluginId,
            ["name"] = "Sample Test Plugin",
            ["version"] = options.Version,
            ["publisher"] = "Darhous",
            ["category"] = "test",
            ["entryPoint"] = "bin/Darhous.TestPlugin.dll",
            ["entryType"] = "Darhous.TestPlugin.TestPlugin",
            ["targetFramework"] = "net10.0",
            ["sdkVersion"] = "1.0",
            ["minCoreVersion"] = options.MinCoreVersion,
            ["trustLevel"] = "developer",
            ["capabilities"] = new[] { "test.capability" },
            ["permissions"] = options.Permissions,
            ["dependencies"] = options.Dependencies,
        };
        var manifestJson = JsonSerializer.Serialize(manifest);
        var manifestBytes = Encoding.UTF8.GetBytes(manifestJson);

        var checksumEntryPath = "bin/Darhous.TestPlugin.dll";
        var actualHash = Convert.ToHexStringLower(SHA256.HashData(dllBytes));
        var checksumHash = options.CorruptChecksum ? new string('0', 64) : actualHash;
        var checksums = new Dictionary<string, string> { [checksumEntryPath] = checksumHash };
        var checksumsJson = JsonSerializer.Serialize(checksums);
        var checksumsBytes = Encoding.UTF8.GetBytes(checksumsJson);

        byte[]? signatureBytes = null;
        if (!options.OmitSignature)
        {
            var certificate = options.SigningCertificate ?? CreateSigningCertificate();
            var contentInfo = new ContentInfo(checksumsBytes);
            var cms = new SignedCms(contentInfo, detached: true);
            cms.ComputeSignature(new CmsSigner(certificate));
            signatureBytes = cms.Encode();
        }

        Directory.CreateDirectory(outputDirectory);
        var packagePath = Path.Combine(outputDirectory, $"{options.PluginId}-{options.Version}.archiveplugin");

        using (var fileStream = new FileStream(packagePath, FileMode.Create))
        using (var zip = new ZipArchive(fileStream, ZipArchiveMode.Create))
        {
            WriteEntry(zip, "manifest.json", manifestBytes);
            WriteEntry(zip, "checksums.json", checksumsBytes);
            if (signatureBytes is not null)
            {
                WriteEntry(zip, "signature.p7s", signatureBytes);
            }

            WriteEntry(zip, checksumEntryPath, dllBytes);
        }

        return packagePath;
    }

    private static void WriteEntry(ZipArchive zip, string entryName, byte[] content)
    {
        var entry = zip.CreateEntry(entryName);
        using var entryStream = entry.Open();
        entryStream.Write(content);
    }
}
