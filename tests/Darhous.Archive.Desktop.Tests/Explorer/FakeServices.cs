using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Documents;
using Darhous.Archive.Core.Permissions;
using Darhous.Archive.Core.Results;
using Darhous.Archive.Modules.Documents.BulkOperations;
using Darhous.Archive.Modules.Documents.Services;
using Darhous.Archive.Modules.Folders;

namespace Darhous.Archive.Desktop.Tests.Explorer;

/// <summary>
/// Minimal fakes — ExplorerViewModel only calls GetChildrenAsync/ListByFolderAsync/ListAsync/
/// TrashDocumentAsync/BulkMoveDocumentsAsync in the paths these tests exercise; everything
/// else throws NotImplementedException on purpose so an unexpected call fails loudly.
/// </summary>
internal sealed class FakeFolderService : IFolderService
{
    // Dictionary<TKey,TValue> never accepts a null key at runtime, even for Nullable<T> —
    // this was caught by CS8714 and confirmed by an actual ArgumentNullException, not a
    // false positive. Guid.Empty stands in for "root" (parentId == null) in this fake only;
    // the real FolderRepository handles NULL natively via SQL, no dictionary involved there.
    private static readonly Guid RootKey = Guid.Empty;

    public Dictionary<Guid, List<Folder>> ChildrenByParent { get; } = [];

    public Task<Result<Guid>> CreateFolderAsync(string name, Guid? parentId, Guid? createdBy, CancellationToken ct) => throw new NotImplementedException();
    public Task<Result> RenameFolderAsync(Guid folderUid, string newName, CancellationToken ct) => throw new NotImplementedException();
    public Task<Result> MoveFolderAsync(Guid folderUid, Guid? newParentId, CancellationToken ct) => throw new NotImplementedException();
    public Task<Result> DeleteFolderAsync(Guid folderUid, bool moveContentsToUnclassified, CancellationToken ct) => throw new NotImplementedException();

    public Task<IReadOnlyList<Folder>> GetChildrenAsync(Guid? parentId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Folder>>(ChildrenByParent.GetValueOrDefault(parentId ?? RootKey, []));
}

internal sealed class FakeDocumentRepository : IDocumentRepository
{
    public List<Document> AllDocuments { get; } = [];

    public Task CreateAsync(Guid uid, NewDocument document, CancellationToken ct) => throw new NotImplementedException();
    public Task<Document?> GetByUidAsync(Guid uid, CancellationToken ct) => throw new NotImplementedException();
    public Task<Document?> GetByArchiveNumberAsync(string archiveNumber, CancellationToken ct) => throw new NotImplementedException();
    public Task SetCurrentVersionAsync(Guid documentUid, Guid versionUid, CancellationToken ct) => throw new NotImplementedException();
    public Task MoveToFolderAsync(Guid documentUid, Guid? folderUid, CancellationToken ct) => throw new NotImplementedException();
    public Task SetStatusAsync(Guid documentUid, DocumentStatus status, CancellationToken ct) => throw new NotImplementedException();
    public Task SoftDeleteAsync(Guid documentUid, DateTimeOffset deletedAt, CancellationToken ct) => throw new NotImplementedException();
    public Task RestoreAsync(Guid documentUid, CancellationToken ct) => throw new NotImplementedException();
    public Task PermanentDeleteAsync(Guid documentUid, CancellationToken ct) => throw new NotImplementedException();

    public Task<IReadOnlyList<Document>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Document>>(AllDocuments);

    public Task<IReadOnlyList<Document>> ListByFolderAsync(Guid? folderId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Document>>(AllDocuments.Where(d => d.FolderId == folderId).ToList());

    public Task<IReadOnlyList<Document>> ListUpdatedSinceAsync(DateTimeOffset since, CancellationToken cancellationToken) => throw new NotImplementedException();
}

internal sealed class FakeDocumentService : IDocumentService
{
    public List<Guid> TrashedDocuments { get; } = [];

    public Task<Result<Guid>> AddDocumentAsync(string sourcePath, string title, Guid? folderId, DocumentSourceType sourceType, DocumentStorageMode storageMode, Guid? createdBy, bool allowDuplicate, CancellationToken ct) => throw new NotImplementedException();
    public Task<Result> MoveDocumentAsync(Guid documentUid, Guid? folderId, Guid? movedBy, CancellationToken ct) => throw new NotImplementedException();
    public Task<Result> RestoreDocumentAsync(Guid documentUid, Guid? restoredBy, CancellationToken ct) => throw new NotImplementedException();
    public Task<Result> PermanentDeleteDocumentAsync(Guid documentUid, UserRole requestedByRole, Guid? requestedBy, CancellationToken ct) => throw new NotImplementedException();

    public Task<Result> TrashDocumentAsync(Guid documentUid, Guid? deletedBy, string? reason, CancellationToken cancellationToken)
    {
        TrashedDocuments.Add(documentUid);
        return Task.FromResult(Result.Success());
    }
}

internal sealed class FakeBulkOperationService : IBulkOperationService
{
    public List<(IReadOnlyList<Guid> Documents, Guid? Target)> Calls { get; } = [];

    public Task<Result<Guid>> BulkMoveDocumentsAsync(IReadOnlyList<Guid> documentUids, Guid? targetFolderId, Guid? movedBy, CancellationToken cancellationToken)
    {
        Calls.Add((documentUids, targetFolderId));
        return Task.FromResult(Result<Guid>.Success(Guid.NewGuid()));
    }

    public Task<Result> UndoAsync(Guid operationSnapshotUid, CancellationToken ct) => throw new NotImplementedException();
}
