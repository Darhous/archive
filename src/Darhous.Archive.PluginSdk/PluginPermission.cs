namespace Darhous.Archive.PluginSdk;

/// <summary>Plugin SDK §76 — the well-known permission strings a manifest can request. Shown to the user before install (§76: "قبل تثبيت Plugin تظهر الصلاحيات المطلوبة").</summary>
public static class PluginPermission
{
    public const string DocumentsRead = "documents.read";
    public const string DocumentsWrite = "documents.write";
    public const string DocumentsDelete = "documents.delete";
    public const string MetadataRead = "metadata.read";
    public const string MetadataWrite = "metadata.write";
    public const string NetworkOutbound = "network.outbound";
    public const string DeviceScan = "device.scan";
    public const string DevicePrint = "device.print";
    public const string SettingsRead = "settings.read";
    public const string SettingsWrite = "settings.write";
    public const string NotificationsSend = "notifications.send";
    public const string SecretsRead = "secrets.read";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        DocumentsRead, DocumentsWrite, DocumentsDelete, MetadataRead, MetadataWrite,
        NetworkOutbound, DeviceScan, DevicePrint, SettingsRead, SettingsWrite, NotificationsSend, SecretsRead,
    };
}
