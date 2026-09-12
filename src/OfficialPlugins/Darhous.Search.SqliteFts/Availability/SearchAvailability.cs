using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Darhous.Search.SqliteFts.Availability;

public sealed class SearchAvailability(ILogger<SearchAvailability> logger) : ISearchAvailability
{
    // SQLite result codes (not exposed as an enum by Microsoft.Data.Sqlite) — see sqlite3.h.
    private const int SqliteCorrupt = 11;
    private const int SqliteNotADb = 26;

    public bool IsAvailable { get; private set; } = true;

    public void MarkAvailable() => IsAvailable = true;

    public void MarkUnavailable(Exception cause)
    {
        if (IsAvailable)
        {
            logger.LogError(cause, "search.db marked unavailable — browsing/other features continue unaffected (DB Spec §97). Rebuild required.");
        }

        IsAvailable = false;
    }

    /// <summary>True for the specific SQLite error codes that mean "the file itself is corrupt", not a transient lock/busy error.</summary>
    public static bool IsCorruption(Exception ex) =>
        ex is SqliteException { SqliteErrorCode: SqliteCorrupt or SqliteNotADb };
}
