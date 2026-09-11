using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Documents;
using Darhous.Archive.Core.Permissions;
using Darhous.Archive.Core.Results;

namespace Darhous.Archive.Modules.Documents.Services;

public interface IDocumentService
{
    Task<Result<Guid>> AddDocumentAsync(
        string sourcePath, string title, Guid? folderId, DocumentSourceType sourceType,
        DocumentStorageMode storageMode, Guid? createdBy, bool allowDuplicate, CancellationToken cancellationToken);

    Task<Result> MoveDocumentAsync(Guid documentUid, Guid? folderId, Guid? movedBy, CancellationToken cancellationToken);

    Task<Result> TrashDocumentAsync(Guid documentUid, Guid? deletedBy, string? reason, CancellationToken cancellationToken);

    Task<Result> RestoreDocumentAsync(Guid documentUid, Guid? restoredBy, CancellationToken cancellationToken);

    /// <summary>SAD §47 (Permanent Delete) — Admin only.</summary>
    Task<Result> PermanentDeleteDocumentAsync(Guid documentUid, UserRole requestedByRole, Guid? requestedBy, CancellationToken cancellationToken);
}
