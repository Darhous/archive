namespace Darhous.Search.SqliteFts.Availability;

/// <summary>
/// DB Spec §97 (Search Index Failure): "البرنامج يظل يعمل، Browsing folders يعمل" — a
/// corrupt search.db must never take down anything outside the search plugin itself. Every
/// search.db read/write goes through <see cref="SearchAvailability.GuardAsync{T}"/>, which
/// traps <c>SQLITE_CORRUPT</c>/<c>SQLITE_NOTADB</c> and flips this flag instead of letting
/// the exception propagate into DocumentService/Explorer.
/// </summary>
public interface ISearchAvailability
{
    bool IsAvailable { get; }

    /// <summary>Set once rebuild recreates the schema from scratch.</summary>
    void MarkAvailable();

    void MarkUnavailable(Exception cause);
}
