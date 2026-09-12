namespace Darhous.Search.SqliteFts.Indexing;

/// <summary>
/// Bumped whenever <c>ArabicNormalization.Normalize</c>'s rules change in a way that affects
/// indexed text. A version bump alone doesn't force anything automatically (no background
/// migration job) — the reconciliation sweep re-indexes a document opportunistically whenever
/// it revisits it, but a full "rebuild now" is the honest fix, exposed to the user, since
/// silently serving stale-normalization results is worse than an explicit rebuild prompt.
/// </summary>
internal static class NormalizerVersion
{
    public const int Current = 1;
}
