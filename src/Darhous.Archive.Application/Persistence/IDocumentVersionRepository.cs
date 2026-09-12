namespace Darhous.Archive.Application.Persistence;

public interface IDocumentVersionRepository
{
    Task<Guid> CreateAsync(NewDocumentVersion version, CancellationToken cancellationToken);

    Task<DocumentVersion?> GetByUidAsync(Guid uid, CancellationToken cancellationToken);

    /// <summary>DB Spec §32 (Deduplication) — SHA-256 exact-match lookup. Null if no version has this hash.</summary>
    Task<DocumentVersion?> FindBySha256Async(string sha256, CancellationToken cancellationToken);

    /// <summary>
    /// Phase 9 (Discovery) — is this filesystem path already a known document version? Checked
    /// BEFORE hashing a discovered file, so a file Discovery has already indexed (Indexed In
    /// Place or Managed) is never re-queued into a duplicate FileIndexJob on the next sweep.
    /// </summary>
    Task<DocumentVersion?> FindByFilePathAsync(string filePath, CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentVersion>> ListForDocumentAsync(Guid documentUid, CancellationToken cancellationToken);

    /// <summary>Phase 10 (Importers) — versions still awaiting text/metadata extraction, oldest first.</summary>
    Task<IReadOnlyList<DocumentVersion>> ListPendingExtractionAsync(int limit, CancellationToken cancellationToken);

    /// <summary>Must run inside <see cref="IUnitOfWork"/> — records the outcome of one extraction attempt.</summary>
    Task UpdateExtractionResultAsync(Guid versionUid, string contentExtractionStatus, int? pageCount, bool? isSearchablePdf, CancellationToken cancellationToken);
}
