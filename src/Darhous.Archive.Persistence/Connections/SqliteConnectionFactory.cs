using Microsoft.Data.Sqlite;
using Darhous.Archive.Persistence.Configuration;

namespace Darhous.Archive.Persistence.Connections;

/// <summary>
/// <c>Cache=Shared</c> is deliberately not used — Microsoft's own guidance is that combining
/// shared cache with WAL is not recommended, and the write queue (not connection-level
/// locking) is what actually serializes writers here.
/// </summary>
public sealed class SqliteConnectionFactory(PersistenceOptions options) : ISqliteConnectionFactory
{
    public async Task<SqliteConnection> OpenAsync(DatabaseKind database, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.DataDirectory);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = options.GetFilePath(database),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Default,
            ForeignKeys = true,
            Pooling = true,
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var pragma = connection.CreateCommand();
        pragma.CommandText = $"PRAGMA busy_timeout = {options.BusyTimeoutMilliseconds};";
        await pragma.ExecuteNonQueryAsync(cancellationToken);

        return connection;
    }
}
