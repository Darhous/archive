namespace Darhous.Archive.Contracts.Audit;

/// <summary>
/// DB Spec §82 declares `action_category` as a plain TEXT column without enumerating values —
/// these groupings follow the natural sections of the action list in DB Spec §83.
/// </summary>
public enum AuditActionCategory
{
    Session,
    Search,
    DocumentView,
    Document,
    Metadata,
    Folder,
    Export,
    Backup,
    Plugin,
    Update,
    Settings,
}
