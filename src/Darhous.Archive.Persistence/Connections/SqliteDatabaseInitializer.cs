using Darhous.Archive.Persistence.Configuration;

namespace Darhous.Archive.Persistence.Connections;

/// <summary>
/// Sets <c>journal_mode=WAL</c> once per database file (it is persisted in the file header,
/// unlike <c>foreign_keys</c>/<c>busy_timeout</c> which are per-connection and reapplied by
/// <see cref="SqliteConnectionFactory"/> on every open). Verifies the engine actually
/// switched to WAL rather than silently continuing in the default rollback-journal mode.
/// </summary>
public sealed class SqliteDatabaseInitializer(ISqliteConnectionFactory connectionFactory)
{
    public async Task InitializeAsync(DatabaseKind database, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(database, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = WAL;";
        var result = await command.ExecuteScalarAsync(cancellationToken);

        var mode = result?.ToString();
        if (!string.Equals(mode, "wal", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Failed to enable WAL mode for {database} — SQLite reported journal_mode='{mode}'. " +
                "This usually means the database file is on a network share or another process holds an incompatible lock.");
        }
    }
}
