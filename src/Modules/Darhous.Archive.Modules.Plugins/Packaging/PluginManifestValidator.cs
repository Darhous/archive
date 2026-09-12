using System.Text.RegularExpressions;
using Darhous.Archive.Modules.Plugins.Versioning;
using Darhous.Archive.PluginSdk;

namespace Darhous.Archive.Modules.Plugins.Packaging;

public sealed record ManifestValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ManifestValidationResult Success() => new(true, []);
    public static ManifestValidationResult Failure(params string[] errors) => new(false, errors);
}

/// <summary>Plugin SDK §9-13 — required-field presence, ID format, and SemVer format. Compatibility (Core/SDK/framework version) is checked separately once we know what's actually installed, not here.</summary>
public static partial class PluginManifestValidator
{
    [GeneratedRegex(@"^[A-Za-z0-9]+(\.[A-Za-z0-9_-]+)+$")]
    private static partial Regex PluginIdPattern();

    public static ManifestValidationResult Validate(PluginManifest manifest)
    {
        var errors = new List<string>();

        void RequireNonEmpty(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"Missing required field: {fieldName}");
            }
        }

        RequireNonEmpty(manifest.SchemaVersion, "schemaVersion");
        RequireNonEmpty(manifest.Id, "id");
        RequireNonEmpty(manifest.Name, "name");
        RequireNonEmpty(manifest.Version, "version");
        RequireNonEmpty(manifest.Publisher, "publisher");
        RequireNonEmpty(manifest.Category, "category");
        RequireNonEmpty(manifest.EntryPoint, "entryPoint");
        RequireNonEmpty(manifest.EntryType, "entryType");
        RequireNonEmpty(manifest.TargetFramework, "targetFramework");
        RequireNonEmpty(manifest.SdkVersion, "sdkVersion");
        RequireNonEmpty(manifest.MinCoreVersion, "minCoreVersion");

        if (manifest.Capabilities.Count == 0)
        {
            errors.Add("capabilities must declare at least one entry.");
        }

        if (!string.IsNullOrWhiteSpace(manifest.Id) && !PluginIdPattern().IsMatch(manifest.Id))
        {
            errors.Add($"Plugin id '{manifest.Id}' does not match the required reverse-DNS-like pattern (Plugin SDK §11).");
        }

        if (!string.IsNullOrWhiteSpace(manifest.Version) && !SemVer.TryParse(manifest.Version, out _))
        {
            errors.Add($"Version '{manifest.Version}' is not valid MAJOR.MINOR.PATCH SemVer.");
        }

        if (!string.IsNullOrWhiteSpace(manifest.MinCoreVersion) && !SemVer.TryParse(manifest.MinCoreVersion, out _))
        {
            errors.Add($"minCoreVersion '{manifest.MinCoreVersion}' is not valid SemVer.");
        }

        foreach (var permission in manifest.Permissions)
        {
            if (!PluginPermission.All.Contains(permission))
            {
                errors.Add($"Unknown permission '{permission}'.");
            }
        }

        if (!string.IsNullOrWhiteSpace(manifest.TrustLevel) && !Enum.TryParse<PluginTrustLevel>(manifest.TrustLevel, ignoreCase: true, out _))
        {
            errors.Add($"Unknown trustLevel '{manifest.TrustLevel}'.");
        }

        return errors.Count == 0 ? ManifestValidationResult.Success() : new ManifestValidationResult(false, errors);
    }
}
