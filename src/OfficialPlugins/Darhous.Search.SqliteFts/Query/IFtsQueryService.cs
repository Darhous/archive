using Darhous.Search.SqliteFts.Contracts;

namespace Darhous.Search.SqliteFts.Query;

public interface IFtsQueryService
{
    /// <summary>
    /// <paramref name="userId"/> is only used for the search-audit entry (DB Spec §44: "يسجل
    /// Query الفعلي فقط، وليس keystrokes") — callers are expected to have already debounced
    /// (250ms) so this is invoked once per settled query, not per keystroke.
    /// </summary>
    Task<SearchResultPage> SearchAsync(SearchQuery query, Guid? userId, CancellationToken cancellationToken);
}
