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

    /// <summary>Phase 15 (OCR) — versions whose PDF text-layer check already determined that OCR is required.</summary>
    Task<IReadOnlyList<DocumentVersion>> ListNeedingOcrAsync(int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Must run inside <see cref="IUnitOfWork"/> — records the outcome of one extraction attempt,
    /// including durable body text. The parent document timestamp is advanced so search reconciliation
    /// observes the new body.
    /// </summary>
    Task UpdateExtractionResultAsync(
        Guid versionUid,
        string contentExtractionStatus,
        int? pageCount,
        bool? isSearchablePdf,
        string? extractedText,
        CancellationToken cancellationToken);

    /// <summary>
    /// Must run inside <see cref="IUnitOfWork"/> — records a successful OCR result without mutating
    /// the original file. <paramref name="searchableFilePath"/> points to a derived archive-storage copy.
    /// </summary>
    Task UpdateOcrResultAsync(
        Guid versionUid,
        string extractedText,
        string searchableFilePath,
        string ocrProvider,
        CancellationToken cancellationToken);
}
