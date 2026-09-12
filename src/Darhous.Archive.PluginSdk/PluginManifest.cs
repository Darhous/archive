namespace Darhous.Archive.PluginSdk;

/// <summary>Plugin SDK §9 — the exact <c>manifest.json</c> shape. Deserialize with a camelCase naming policy to match the field names verbatim.</summary>
public sealed record PluginManifest(
    string SchemaVersion,
    string Id,
    string Name,
    string? DisplayNameAr,
    string? Description,
    string Version,
    string Publisher,
    string? PublisherId,
    string Category,
    string EntryPoint,
    string EntryType,
    string TargetFramework,
    string SdkVersion,
    string MinCoreVersion,
    string? MaxCoreVersion,
    string? Isolation,
    bool RequiresRestart,
    string TrustLevel,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<PluginDependency>? Dependencies = null,
    IReadOnlyList<PluginDependency>? OptionalDependencies = null,
    string? UpdateChannel = null,
    string? Homepage = null,
    string? SupportUrl = null,
    string? License = null,
    string? MinimumHostApi = null);

/// <summary>Plugin SDK §42.</summary>
public sealed record PluginDependency(string Id, string Version);
