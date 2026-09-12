namespace Darhous.Search.SqliteFts.Rebuild;

/// <summary>DB Spec §96/§157 (Rebuild, Search Corruption Recovery) — the user-triggered "rebuild index" action.</summary>
public interface ISearchIndexRebuilder
{
    Task RebuildAsync(CancellationToken cancellationToken);
}
