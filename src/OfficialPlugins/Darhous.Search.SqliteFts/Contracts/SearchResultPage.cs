namespace Darhous.Search.SqliteFts.Contracts;

public sealed record SearchResultPage(IReadOnlyList<SearchHit> Hits, int TotalCount, int Page, int PageSize)
{
    /// <summary>Search ran but the index is unavailable (corruption — DB Spec §97) rather than genuinely zero matches.</summary>
    public bool IsUnavailable { get; init; }
}
