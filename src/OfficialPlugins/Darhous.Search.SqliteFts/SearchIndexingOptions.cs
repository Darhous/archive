namespace Darhous.Search.SqliteFts;

/// <summary>
/// SAD §128 (Eventual Consistency) — search.db is allowed a short, bounded delay behind
/// archive.db by design; these two intervals are what bound it in practice.
/// </summary>
public sealed class SearchIndexingOptions
{
    /// <summary>How often the incremental sweep (documents.updated_at &gt; checkpoint) runs.</summary>
    public TimeSpan ReconciliationInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Every Nth reconciliation tick also runs a full diff pass (search_state vs. every
    /// active document uid) to catch permanent deletes — the incremental sweep can't see a
    /// row that no longer exists at all, only rows whose updated_at changed.
    /// </summary>
    public int FullSweepEveryNTicks { get; set; } = 12;
}
