using Darhous.Archive.Core.Results;

namespace Darhous.Archive.Modules.Documents.BulkOperations;

/// <summary>DB Spec §139/§141 (Bulk Move + Undo Support).</summary>
public interface IBulkOperationService
{
    /// <summary>Returns the operation-snapshot uid, usable with <see cref="UndoAsync"/> within the undo window.</summary>
    Task<Result<Guid>> BulkMoveDocumentsAsync(
        IReadOnlyList<Guid> documentUids, Guid? targetFolderId, Guid? movedBy, CancellationToken cancellationToken);

    /// <summary>Fails if the snapshot is unknown, already reverted, or past its expiry (default 30s).</summary>
    Task<Result> UndoAsync(Guid operationSnapshotUid, CancellationToken cancellationToken);
}
