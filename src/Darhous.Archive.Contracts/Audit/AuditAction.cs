namespace Darhous.Archive.Contracts.Audit;

/// <summary>DB Spec §83 (Audit Actions) — every value the `action` column is expected to hold.</summary>
public static class AuditAction
{
    public const string Login = "login";
    public const string Logout = "logout";
    public const string SwitchUser = "switch_user";
    public const string FailedLogin = "failed_login";

    public const string Search = "search";
    public const string Sort = "sort";
    public const string Filter = "filter";
    public const string ChangeView = "change_view";

    public const string OpenDocument = "open_document";
    public const string PreviewDocument = "preview_document";

    public const string AddDocument = "add_document";
    public const string ScanDocument = "scan_document";
    public const string ImportDocument = "import_document";
    public const string RenameDocument = "rename_document";
    public const string MoveDocument = "move_document";
    public const string BulkMove = "bulk_move";
    public const string DeleteDocument = "delete_document";
    public const string RestoreDocument = "restore_document";
    public const string PermanentDelete = "permanent_delete";

    public const string EditMetadata = "edit_metadata";
    public const string AddTag = "add_tag";
    public const string RemoveTag = "remove_tag";

    public const string CreateFolder = "create_folder";
    public const string RenameFolder = "rename_folder";
    public const string MoveFolder = "move_folder";
    public const string DeleteFolder = "delete_folder";

    public const string Export = "export";
    public const string Print = "print";

    public const string Backup = "backup";
    public const string Restore = "restore";

    public const string PluginInstall = "plugin_install";
    public const string PluginEnable = "plugin_enable";
    public const string PluginDisable = "plugin_disable";
    public const string PluginUpdate = "plugin_update";
    public const string PluginRemove = "plugin_remove";

    public const string AppUpdate = "app_update";

    public const string SettingsChange = "settings_change";

    /// <summary>DB Spec §106 (Audit Critical Events) — written immediately, never buffered.</summary>
    public static readonly IReadOnlySet<string> CriticalActions = new HashSet<string>
    {
        Login, FailedLogin, DeleteDocument, PermanentDelete, DeleteFolder, RestoreDocument,
        SettingsChange, PluginInstall, PluginRemove, Backup, Restore, AppUpdate,
    };
}
