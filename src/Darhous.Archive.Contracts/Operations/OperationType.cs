namespace Darhous.Archive.Contracts.Operations;

/// <summary>DB Spec §141 (Undo Support) — only bulk operations get a snapshot; never Permanent Delete, Restore, or Plugin install.</summary>
public static class OperationType
{
    public const string BulkMove = "bulk_move";
    public const string TagsBulkUpdate = "tags_bulk_update";
}
