namespace Darhous.Archive.Application.Persistence;

public interface ISourceExclusionRepository
{
    /// <summary>No-op if an exclusion with the same normalized path already exists (idempotent — safe to call on every startup for the technical defaults).</summary>
    Task<Guid> CreateIfMissingAsync(NewSourceExclusion exclusion, CancellationToken cancellationToken);

    Task<IReadOnlyList<SourceExclusion>> ListEnabledAsync(CancellationToken cancellationToken);

    Task RemoveAsync(Guid uid, CancellationToken cancellationToken);
}
