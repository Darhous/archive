using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Audit;
using Darhous.Archive.Core.Audit;
using Darhous.Archive.Core.Results;
using Darhous.Archive.Core.Text;

namespace Darhous.Archive.Modules.Folders;

public sealed class FolderService(IUnitOfWork unitOfWork, IAuditService auditService) : IFolderService
{
    public async Task<Result<Guid>> CreateFolderAsync(string name, Guid? parentId, Guid? createdBy, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<Guid>.Failure(Error.Of("FOLDER_NAME_REQUIRED", "اسم الفولدر مطلوب."));
        }

        var duplicate = await FindSiblingWithNameAsync(parentId, name, cancellationToken);
        if (duplicate is not null)
        {
            return Result<Guid>.Failure(Error.Of("FOLDER_NAME_TAKEN", "يوجد فولدر بنفس الاسم في نفس المستوى بالفعل."));
        }

        var siblingCount = (await unitOfWork.ExecuteAsync((context, ct) => context.Folders.ListChildrenAsync(parentId, ct), cancellationToken)).Count;

        var uid = await unitOfWork.ExecuteAsync(
            (context, ct) => context.Folders.CreateAsync(new NewFolder(parentId, name, siblingCount, createdBy), ct),
            cancellationToken);

        await auditService.RecordAsync(
            new AuditEntry(AuditAction.CreateFolder, AuditActionCategory.Folder, AuditResult.Success,
                UserId: createdBy, EntityType: "folder", EntityUid: uid.ToString(), EntityNameSnapshot: name),
            cancellationToken);

        return Result<Guid>.Success(uid);
    }

    public async Task<Result> RenameFolderAsync(Guid folderUid, string newName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return Result.Failure(Error.Of("FOLDER_NAME_REQUIRED", "اسم الفولدر مطلوب."));
        }

        var folder = await unitOfWork.ExecuteAsync((context, ct) => context.Folders.GetByUidAsync(folderUid, ct), cancellationToken);
        if (folder is null)
        {
            return Result.Failure(Error.Of("FOLDER_NOT_FOUND", "الفولدر غير موجود."));
        }

        var duplicate = await FindSiblingWithNameAsync(folder.ParentId, newName, cancellationToken);
        if (duplicate is not null && duplicate.Uid != folderUid)
        {
            return Result.Failure(Error.Of("FOLDER_NAME_TAKEN", "يوجد فولدر بنفس الاسم في نفس المستوى بالفعل."));
        }

        await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.Folders.RenameAsync(folderUid, newName, ct);
            return null;
        }, cancellationToken);

        await auditService.RecordAsync(
            new AuditEntry(AuditAction.RenameFolder, AuditActionCategory.Folder, AuditResult.Success,
                EntityType: "folder", EntityUid: folderUid.ToString(), EntityNameSnapshot: newName),
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> MoveFolderAsync(Guid folderUid, Guid? newParentId, CancellationToken cancellationToken)
    {
        if (newParentId == folderUid)
        {
            return Result.Failure(Error.Of("FOLDER_MOVE_CYCLE", "لا يمكن نقل الفولدر داخل نفسه."));
        }

        if (newParentId is not null)
        {
            var allFolders = await unitOfWork.ExecuteAsync((context, ct) => context.Folders.ListAllAsync(ct), cancellationToken);
            var byUid = allFolders.ToDictionary(f => f.Uid);

            // Walk up from the proposed new parent; if we ever reach folderUid, this move
            // would make the folder its own ancestor (SAD §38: "منع cycle").
            var current = newParentId;
            while (current is { } currentUid && byUid.TryGetValue(currentUid, out var currentFolder))
            {
                if (currentUid == folderUid)
                {
                    return Result.Failure(Error.Of("FOLDER_MOVE_CYCLE", "لا يمكن نقل الفولدر إلى داخل أحد فولدراته الفرعية."));
                }

                current = currentFolder.ParentId;
            }
        }

        await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.Folders.MoveAsync(folderUid, newParentId, ct);
            return null;
        }, cancellationToken);

        await auditService.RecordAsync(
            new AuditEntry(AuditAction.MoveFolder, AuditActionCategory.Folder, AuditResult.Success,
                EntityType: "folder", EntityUid: folderUid.ToString()),
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> DeleteFolderAsync(Guid folderUid, bool moveContentsToUnclassified, CancellationToken cancellationToken)
    {
        var folder = await unitOfWork.ExecuteAsync((context, ct) => context.Folders.GetByUidAsync(folderUid, ct), cancellationToken);
        if (folder is null)
        {
            return Result.Failure(Error.Of("FOLDER_NOT_FOUND", "الفولدر غير موجود."));
        }

        var hasChildren = await unitOfWork.ExecuteAsync((context, ct) => context.Folders.HasChildrenAsync(folderUid, ct), cancellationToken);
        var documentCount = await unitOfWork.ExecuteAsync((context, ct) => context.Folders.CountDocumentsAsync(folderUid, ct), cancellationToken);

        if ((hasChildren || documentCount > 0) && !moveContentsToUnclassified)
        {
            return Result.Failure(Error.Of(
                "FOLDER_NOT_EMPTY",
                $"الفولدر يحتوي على {documentCount} مستند و{(hasChildren ? "فولدرات فرعية" : "لا فولدرات فرعية")} — " +
                "لا يمكن حذفه إلا بعد إفراغه أو نقل محتواه (DB Spec §137)."));
        }

        await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            if (moveContentsToUnclassified)
            {
                var children = await context.Folders.ListChildrenAsync(folderUid, ct);
                foreach (var child in children)
                {
                    await context.Folders.MoveAsync(child.Uid, null, ct);
                }

                // Documents move to Unclassified via each document's own MoveToFolderAsync —
                // handled by IDocumentService normally, but a folder delete cascades this
                // directly here since it's a folder-owned operation, not a document one.
                // ListByFolderAsync uses the folder_id index (DB Spec §99) — never load every
                // document in the archive just to find the handful in this one folder.
                var documents = await context.Documents.ListByFolderAsync(folderUid, ct);
                foreach (var document in documents)
                {
                    await context.Documents.MoveToFolderAsync(document.Uid, null, ct);
                }
            }

            await context.Folders.DeleteAsync(folderUid, ct);
            return null;
        }, cancellationToken);

        await auditService.RecordAsync(
            new AuditEntry(AuditAction.DeleteFolder, AuditActionCategory.Folder, AuditResult.Success,
                EntityType: "folder", EntityUid: folderUid.ToString(), EntityNameSnapshot: folder.Name),
            cancellationToken);

        return Result.Success();
    }

    public Task<IReadOnlyList<Folder>> GetChildrenAsync(Guid? parentId, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync((context, ct) => context.Folders.ListChildrenAsync(parentId, ct), cancellationToken);

    private async Task<Folder?> FindSiblingWithNameAsync(Guid? parentId, string name, CancellationToken cancellationToken)
    {
        var normalized = ArabicNormalization.Normalize(name);
        var siblings = await unitOfWork.ExecuteAsync((context, ct) => context.Folders.ListChildrenAsync(parentId, ct), cancellationToken);
        return siblings.FirstOrDefault(f => ArabicNormalization.Normalize(f.Name) == normalized);
    }
}
