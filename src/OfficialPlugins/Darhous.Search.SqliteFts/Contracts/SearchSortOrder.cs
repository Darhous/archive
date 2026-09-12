namespace Darhous.Search.SqliteFts.Contracts;

public enum SearchSortOrder
{
    /// <summary>Default — FTS5 bm25() rank, best match first (DB Spec §92).</summary>
    Relevance,
    DateDescending,
    DateAscending,
    TitleAscending,
}
