using System.Text.Json;
using Darhous.Archive.Modules.Plugins.Versioning;
using Darhous.Archive.PluginSdk;

namespace Darhous.Archive.Modules.Plugins.Packaging;

public sealed record PluginValidationResult(bool IsValid, PluginManifest? Manifest, string? PackageHash, IReadOnlyList<string> Errors)
{
    public static PluginValidationResult Failure(params string[] errors) => new(false, null, null, errors);
}

/// <summary>
/// Plugin SDK §38 — "Package Validation Order", run in exactly this sequence so the first
/// failure reported is always the most fundamental one (a corrupt ZIP is reported as that,
/// never as a confusing downstream signature error).
/// </summary>
public sealed class PluginPackageValidator(IPluginTrustStore trustStore, Version coreVersion)
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new(JsonSerializerDefaults.Web);

    public PluginValidationResult Validate(string packagePath)
    {
        PluginPackage package;
        try
        {
            package = PluginPackage.Open(packagePath);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            return PluginValidationResult.Failure($"Package is not a valid ZIP archive: {ex.Message}");
        }

        using (package)
        {
            PluginManifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<PluginManifest>(package.RawManifestJson, ManifestJsonOptions)
                    ?? throw new InvalidDataException("manifest.json deserialized to null.");
            }
            catch (Exception ex) when (ex is InvalidDataException or JsonException)
            {
                return PluginValidationResult.Failure($"manifest.json is invalid: {ex.Message}");
            }

            var manifestResult = PluginManifestValidator.Validate(manifest);
            if (!manifestResult.IsValid)
            {
                return new PluginValidationResult(false, manifest, null, manifestResult.Errors);
            }

            var checksumErrors = PluginChecksumVerifier.Verify(package);
            if (checksumErrors.Count > 0)
            {
                return new PluginValidationResult(false, manifest, null, checksumErrors);
            }

            var signatureErrors = PluginSignatureVerifier.Verify(package, trustStore);
            if (signatureErrors.Count > 0)
            {
                return new PluginValidationResult(false, manifest, null, signatureErrors);
            }

            var compatibilityErrors = CheckCompatibility(manifest);
            if (compatibilityErrors.Count > 0)
            {
                return new PluginValidationResult(false, manifest, null, compatibilityErrors);
            }

            var packageHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(packagePath)));
            return new PluginValidationResult(true, manifest, packageHash, []);
        }
    }

    private IReadOnlyList<string> CheckCompatibility(PluginManifest manifest)
    {
        var errors = new List<string>();

        if (SemVer.TryParse(manifest.MinCoreVersion, out var minCore))
        {
            var runningCore = new SemVer(coreVersion.Major, coreVersion.Minor, Math.Max(coreVersion.Build, 0));
            if (runningCore < minCore)
            {
                errors.Add($"Plugin requires Core >= {manifest.MinCoreVersion}, running Core is {runningCore} (Plugin SDK §13 — Incompatible).");
            }
        }

        if (!string.IsNullOrWhiteSpace(manifest.MaxCoreVersion) && manifest.MaxCoreVersion.EndsWith(".x", StringComparison.Ordinal))
        {
            var maxMajorText = manifest.MaxCoreVersion[..manifest.MaxCoreVersion.IndexOf('.')];
            if (int.TryParse(maxMajorText, out var maxMajor) && coreVersion.Major > maxMajor)
            {
                errors.Add($"Plugin supports Core {manifest.MaxCoreVersion}, running Core major version is {coreVersion.Major} (Plugin SDK §13 — Incompatible).");
            }
        }

        return errors;
    }
}
