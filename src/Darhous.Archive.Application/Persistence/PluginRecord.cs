namespace Darhous.Archive.Application.Persistence;

/// <summary>DB Spec §66 (plugins) — public shape. String enums (TrustLevel/Status) rather than the SDK's own enum types, since Application must not depend on the PluginSdk project.</summary>
public sealed record PluginRecord(
    string PluginId,
    string InstalledVersion,
    string ActiveVersion,
    string Publisher,
    string TrustLevel,
    string Status,
    string UpdateChannel,
    string PackageHash,
    int CrashCount,
    DateTimeOffset InstalledAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastHealthAt);

public sealed record NewPluginRecord(
    string PluginId, string InstalledVersion, string ActiveVersion, string Publisher,
    string TrustLevel, string Status, string UpdateChannel, string PackageHash);

/// <summary>DB Spec §67 (plugin_permissions).</summary>
public sealed record PluginPermissionRecord(string PluginId, string Permission, bool Granted, Guid? GrantedBy, DateTimeOffset? GrantedAt);
