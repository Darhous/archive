using Microsoft.Data.Sqlite;
using Darhous.Archive.Persistence.Configuration;

namespace Darhous.Archive.Persistence.Connections;

/// <summary>
/// Opens connections to one of the three SQLite files. Callers that only read should use
/// this directly (WAL allows concurrent readers); writers must go through
/// <see cref="Writes.ISqliteWriteQueue"/> instead of opening their own write connection.
/// </summary>
public interface ISqliteConnectionFactory
{
    /// <summary>Opens a new connection with WAL + foreign_keys=ON + busy_timeout already applied.</summary>
    Task<SqliteConnection> OpenAsync(DatabaseKind database, CancellationToken cancellationToken);
}
