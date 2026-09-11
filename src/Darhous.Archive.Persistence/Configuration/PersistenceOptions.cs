using Darhous.Archive.Configuration;

namespace Darhous.Archive.Persistence.Configuration;

/// <summary>
/// Where the three SQLite files live. Defaults to <see cref="AppPaths.Data"/>
/// (%ProgramData%\DarhousSmartArchive\Data); tests override <see cref="DataDirectory"/>
/// to an isolated temp directory so runs never touch the real machine state.
/// </summary>
public sealed class PersistenceOptions
{
    public string DataDirectory { get; init; } = AppPaths.Data;

    /// <summary>Milliseconds SQLite waits on a locked database before returning SQLITE_BUSY.</summary>
    public int BusyTimeoutMilliseconds { get; init; } = 5000;

    public string GetFilePath(DatabaseKind database) => Path.Combine(DataDirectory, database switch
    {
        DatabaseKind.Archive => "archive.db",
        DatabaseKind.Audit => "audit.db",
        DatabaseKind.Search => "search.db",
        _ => throw new ArgumentOutOfRangeException(nameof(database)),
    });
}
