using Darhous.Archive.Configuration;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Writes;

namespace Darhous.Archive.Persistence.Configuration;

/// <summary>SQLite-backed implementation of the existing app_settings contract.</summary>
public sealed class SqliteAppSettingsStore(
    ISqliteConnectionFactory connectionFactory,
    ISqliteWriteQueue archiveWriteQueue,
    IClock clock) : IAppSettingsStore
{
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await using var connection = await connectionFactory.OpenAsync(DatabaseKind.Archive, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value_json FROM app_settings WHERE setting_key = @key;";
        command.Parameters.AddWithValue("@key", key);
        return (string?)await command.ExecuteScalarAsync(cancellationToken);
    }

    public Task SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        return archiveWriteQueue.EnqueueAsync(async (connection, transaction, ct) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO app_settings (setting_key, value_json, updated_at)
                VALUES (@key, @value, @updatedAt)
                ON CONFLICT(setting_key) DO UPDATE SET
                    value_json = excluded.value_json,
                    updated_at = excluded.updated_at;
                """;
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@value", value);
            command.Parameters.AddWithValue("@updatedAt", clock.UtcNow.ToUnixTimeMilliseconds());
            await command.ExecuteNonQueryAsync(ct);
            return true;
        }, cancellationToken);
    }
}
