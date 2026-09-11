namespace Darhous.Archive.Core.Permissions;

/// <summary>
/// A named capability (SAD §75 Plugin Permissions — also used for host-side authorization
/// checks). Modeled as a value wrapping a stable string so new permissions never require a
/// shared enum change across Core, Contracts and every plugin.
/// </summary>
public readonly record struct Permission(string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// Catalog of permissions defined by SAD §75. Third-party plugins declare a subset of these
/// in their manifest; the Plugin Host (Phase 12) validates against this list before install.
/// </summary>
public static class WellKnownPermissions
{
    public static readonly Permission DocumentsRead = new("documents.read");
    public static readonly Permission DocumentsWrite = new("documents.write");
    public static readonly Permission DocumentsDelete = new("documents.delete");
    public static readonly Permission MetadataRead = new("metadata.read");
    public static readonly Permission MetadataWrite = new("metadata.write");
    public static readonly Permission NetworkOutbound = new("network.outbound");
    public static readonly Permission DeviceScan = new("device.scan");
    public static readonly Permission DevicePrint = new("device.print");
    public static readonly Permission SettingsRead = new("settings.read");
    public static readonly Permission SettingsWrite = new("settings.write");
    public static readonly Permission NotificationsSend = new("notifications.send");
}
