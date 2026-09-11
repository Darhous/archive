using Microsoft.Data.Sqlite;

namespace Darhous.Archive.Persistence.Writes;

/// <summary>
/// The single writer for one SQLite database file. Every mutation goes through
/// <see cref="EnqueueAsync{TResult}"/> instead of opening its own write connection —
/// this is what structurally prevents SQLITE_BUSY between the UI, background jobs, and
/// (later) workers, rather than relying on <c>busy_timeout</c> retries (Phase 2 design
/// review). Reads bypass this entirely and use <see cref="Connections.ISqliteConnectionFactory"/>
/// directly — WAL allows concurrent readers alongside the one writer.
/// </summary>
public interface ISqliteWriteQueue
{
    Task<TResult> EnqueueAsync<TResult>(
        Func<SqliteConnection, SqliteTransaction, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}
