using Darhous.Archive.Contracts.Documents;

namespace Darhous.Archive.Application.Persistence;

public interface IDocumentRepository
{
    /// <summary>
    /// The caller decides <paramref name="uid"/> up front (rather than the repository
    /// generating it) because for managed storage the physical file path is built from the
    /// document's uid before the DB row exists — they must be the same value.
    /// </summary>
    Task CreateAsync(Guid uid, NewDocument document, CancellationToken cancellationToken);

    Task<Document?> GetByUidAsync(Guid uid, CancellationToken cancellationToken);

    Task<Document?> GetByArchiveNumberAsync(string archiveNumber, CancellationToken cancellationToken);

    Task<IReadOnlyList<Document>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Non-deleted documents directly in this folder (or Unclassified, if null) — indexed lookup, not a table scan (DB Spec §99: ix_documents_folder_status).</summary>
    Task<IReadOnlyList<Document>> ListByFolderAsync(Guid? folderId, CancellationToken cancellationToken);

    /// <summary>
    /// Documents (including soft-deleted ones — callers must check <see cref="Document.DeletedAt"/>)
    /// whose <c>updated_at</c> is strictly greater than <paramref name="since"/>, oldest first. Drives
    /// the Phase 8 search-index reconciliation sweep (<c>ix_documents_updated_at</c>, DB Spec §99);
    /// never used for anything requiring a hard consistency guarantee.
    /// </summary>
    Task<IReadOnlyList<Document>> ListUpdatedSinceAsync(DateTimeOffset since, CancellationToken cancellationToken);

    Task SetCurrentVersionAsync(Guid documentUid, Guid versionUid, CancellationToken cancellationToken);

    Task MoveToFolderAsync(Guid documentUid, Guid? folderUid, CancellationToken cancellationToken);

    Task SetStatusAsync(Guid documentUid, DocumentStatus status, CancellationToken cancellationToken);

    /// <summary>SAD §19 — moves the document to the Recycle Bin (documents.deleted_at set). Does not touch files.</summary>
    Task SoftDeleteAsync(Guid documentUid, DateTimeOffset deletedAt, CancellationToken cancellationToken);

    /// <summary>Clears deleted_at and restores status to Active.</summary>
    Task RestoreAsync(Guid documentUid, CancellationToken cancellationToken);

    /// <summary>Removes the row (and, via ON DELETE CASCADE, its versions/tags). Caller must have already deleted the managed files.</summary>
    Task PermanentDeleteAsync(Guid documentUid, CancellationToken cancellationToken);
}
