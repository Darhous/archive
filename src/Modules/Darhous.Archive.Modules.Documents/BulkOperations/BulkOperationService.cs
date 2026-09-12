using System.Text.Json;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Audit;
using Darhous.Archive.Contracts.Operations;
using Darhous.Archive.Core.Audit;
using Darhous.Archive.Core.Results;
using Darhous.Archive.Core.Time;

namespace Darhous.Archive.Modules.Documents.BulkOperations;

public sealed class BulkOperationService(IUnitOfWork unitOfWork, IClock clock, IAuditService auditService) : IBulkOperationService
{
    private static readonly TimeSpan UndoWindow = TimeSpan.FromSeconds(30);

    public async Task<Result<Guid>> BulkMoveDocumentsAsync(
        IReadOnlyList<Guid> documentUids, Guid? targetFolderId, Guid? movedBy, CancellationToken cancellationToken)
    {
        if (documentUids.Count == 0)
        {
            return Result<Guid>.Failure(Error.Of("BULK_MOVE_EMPTY", "لا يوجد مستندات محددة للنقل."));
        }

        var now = clock.UtcNow;

        var snapshotUid = await unitOfWork.ExecuteAsync(async (context, ct) =>
        {
            var entries = new List<BulkMoveSnapshotEntry>(documentUids.Count);

            foreach (var documentUid in documentUids)
            {
                var document = await context.Documents.GetByUidAsync(documentUid, ct);
                if (document is null)
                {
                    continue;
                }

                entries.Add(new BulkMoveSnapshotEntry(documentUid, document.FolderId));
                await context.Documents.MoveToFolderAsync(documentUid, targetFolderId, ct);
            }

            var payloadJson = JsonSerializer.Serialize(entries);
            return await context.OperationSnapshots.CreateAsync(
                movedBy, OperationType.BulkMove, payloadJson, now.Add(UndoWindow), ct);
        }, cancellationToken);

        await auditService.RecordAsync(
            new AuditEntry(
                AuditAction.BulkMove, AuditActionCategory.Document, AuditResult.Success,
                UserId: movedBy, EntityType: "operation_snapshot", EntityUid: snapshotUid.ToString(),
                Details: $"{documentUids.Count} document(s)"),
            cancellationToken);

        return Result<Guid>.Success(snapshotUid);
    }

    public async Task<Result> UndoAsync(Guid operationSnapshotUid, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        return await unitOfWork.ExecuteAsync(async (context, ct) =>
        {
            var snapshot = await context.OperationSnapshots.GetByUidAsync(operationSnapshotUid, ct);
            if (snapshot is null)
            {
                return Result.Failure(Error.Of("UNDO_SNAPSHOT_NOT_FOUND", "لا يوجد عملية بهذا المعرف."));
            }

            if (!snapshot.CanUndo(now))
            {
                return Result.Failure(Error.Of("UNDO_WINDOW_EXPIRED", "انتهت مهلة التراجع (30 ثانية) أو تم التراجع بالفعل."));
            }

            if (snapshot.OperationType != OperationType.BulkMove)
            {
                return Result.Failure(Error.Of("UNDO_UNSUPPORTED_OPERATION", "نوع العملية غير مدعوم للتراجع."));
            }

            var entries = JsonSerializer.Deserialize<List<BulkMoveSnapshotEntry>>(snapshot.PayloadJson) ?? [];
            foreach (var entry in entries)
            {
                await context.Documents.MoveToFolderAsync(entry.DocumentUid, entry.PreviousFolderId, ct);
            }

            await context.OperationSnapshots.MarkRevertedAsync(operationSnapshotUid, now, ct);
            return Result.Success();
        }, cancellationToken);
    }
}
