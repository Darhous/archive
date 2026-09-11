namespace Darhous.Archive.Application.Persistence;

public interface IDocumentVersionRepository
{
    Task<Guid> CreateAsync(NewDocumentVersion version, CancellationToken cancellationToken);

    Task<DocumentVersion?> GetByUidAsync(Guid uid, CancellationToken cancellationToken);

    /// <summary>DB Spec §32 (Deduplication) — SHA-256 exact-match lookup. Null if no version has this hash.</summary>
    Task<DocumentVersion?> FindBySha256Async(string sha256, CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentVersion>> ListForDocumentAsync(Guid documentUid, CancellationToken cancellationToken);
}
