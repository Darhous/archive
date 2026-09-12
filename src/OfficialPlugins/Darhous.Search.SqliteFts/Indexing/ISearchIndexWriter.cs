namespace Darhous.Search.SqliteFts.Indexing;

/// <summary>
/// Upserts/removes a single document's row in <c>search_documents</c>/<c>documents_fts</c>/
/// <c>search_state</c> through the Search database's write queue. Never throws for a
/// search.db-specific failure (corruption) — see <c>Availability.ISearchAvailability</c>;
/// it flips availability and returns instead, so a bad search index never blocks indexing
/// of the next document or any archive.db-side caller.
/// </summary>
public interface ISearchIndexWriter
{
    Task UpsertAsync(SearchDocumentSnapshot snapshot, CancellationToken cancellationToken);

    Task RemoveAsync(Guid documentUid, CancellationToken cancellationToken);
}
