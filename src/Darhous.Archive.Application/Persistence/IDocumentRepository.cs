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
