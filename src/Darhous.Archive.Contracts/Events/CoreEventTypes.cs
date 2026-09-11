namespace Darhous.Archive.Contracts.Events;

/// <summary>
/// Canonical <c>EventType</c> names for <see cref="ArchiveEventEnvelope{T}"/> (SAD §55, Plugin SDK §54).
/// Payload DTOs are added per-module as each module is implemented (Documents in Phase 5,
/// Folders in Phase 6, ...) — this file only reserves the wire names so producers and
/// consumers agree on them from day one.
/// </summary>
public static class CoreEventTypes
{
    public const string DocumentAdded = "DocumentAdded";
    public const string DocumentUpdated = "DocumentUpdated";
    public const string DocumentDeleted = "DocumentDeleted";
    public const string DocumentRestored = "DocumentRestored";
    public const string DocumentVersionAdded = "DocumentVersionAdded";

    public const string FolderCreated = "FolderCreated";
    public const string FolderUpdated = "FolderUpdated";
    public const string FolderDeleted = "FolderDeleted";

    public const string SearchExecuted = "SearchExecuted";

    public const string ScanCompleted = "ScanCompleted";
    public const string OcrCompleted = "OcrCompleted";
    public const string OcrFailed = "OcrFailed";

    public const string IndexCompleted = "IndexCompleted";
    public const string IndexFailed = "IndexFailed";

    public const string BackupCompleted = "BackupCompleted";
    public const string BackupFailed = "BackupFailed";

    public const string ExportCompleted = "ExportCompleted";

    public const string UserLoggedIn = "UserLoggedIn";
    public const string UserLoggedOut = "UserLoggedOut";

    public const string PluginInstalled = "PluginInstalled";
    public const string PluginUpdated = "PluginUpdated";
    public const string PluginFailed = "PluginFailed";

    public const string ApplicationStarted = "ApplicationStarted";
    public const string ApplicationStopping = "ApplicationStopping";
}
