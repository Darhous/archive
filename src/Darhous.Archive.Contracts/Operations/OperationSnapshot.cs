namespace Darhous.Archive.Contracts.Operations;

/// <summary>DB Spec §142 (operation_snapshots).</summary>
public sealed record OperationSnapshot(
    Guid Uid,
    Guid? UserId,
    string OperationType,
    string PayloadJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevertedAt)
{
    public bool CanUndo(DateTimeOffset now) => RevertedAt is null && ExpiresAt > now;
}

/// <summary>Payload for <see cref="Contracts.Operations.OperationType.BulkMove"/> — one entry per moved document.</summary>
public sealed record BulkMoveSnapshotEntry(Guid DocumentUid, Guid? PreviousFolderId);
