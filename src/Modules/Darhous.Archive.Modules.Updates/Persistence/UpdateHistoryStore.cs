using Darhous.Archive.Core.Time;
using Darhous.Archive.Modules.Updates.Packaging;
using Darhous.Archive.Persistence.Writes;

namespace Darhous.Archive.Modules.Updates.Persistence;

internal sealed record UpdateHistoryEntry(
    long Id,
    string ComponentType,
    string ComponentId,
    string? FromVersion,
    string ToVersion,
    string Status,
    string? RollbackVersion,
    string? ErrorMessage);

internal sealed class UpdateHistoryStore(ISqliteWriteQueue archiveWriteQueue, IClock clock)
{
    public Task<long> StartAsync(
        UpdatePackageManifest manifest,
        Version fromVersion,
        Guid? initiatedBy,
        CancellationToken cancellationToken) =>
        archiveWriteQueue.EnqueueAsync(async (connection, transaction, ct) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO update_history
                    (component_type, component_id, from_version, to_version, status,
                     initiated_by, started_at)
                VALUES
                    (@componentType, @componentId, @fromVersion, @toVersion, 'running',
                     (SELECT id FROM app_users WHERE uid = @initiatedBy), @startedAt);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("@componentType", manifest.ComponentType);
            command.Parameters.AddWithValue("@componentId", manifest.ComponentId);
            command.Parameters.AddWithValue("@fromVersion", fromVersion.ToString());
            command.Parameters.AddWithValue("@toVersion", manifest.Version);
            command.Parameters.AddWithValue("@initiatedBy", initiatedBy?.ToString() ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@startedAt", clock.UtcNow.ToUnixTimeMilliseconds());
            return (long)(await command.ExecuteScalarAsync(ct))!;
        }, cancellationToken);

    public Task CompleteAsync(long id, Version rollbackVersion, CancellationToken cancellationToken) =>
        UpdateAsync(id, "completed", rollbackVersion.ToString(), null, cancellationToken);

    public Task FailAsync(long id, Exception exception, CancellationToken cancellationToken) =>
        UpdateAsync(id, "failed", null, exception.Message, cancellationToken);

    public Task RolledBackAsync(long id, CancellationToken cancellationToken) =>
        UpdateAsync(id, "rolled_back", null, null, cancellationToken);

    public Task RollbackFailedAsync(long id, Exception exception, CancellationToken cancellationToken) =>
        UpdateAsync(id, "rollback_failed", null, exception.Message, cancellationToken);

    public Task<UpdateHistoryEntry?> GetAsync(long id, CancellationToken cancellationToken) =>
        ReadAsync("WHERE id = @id", id, cancellationToken);

    public Task<UpdateHistoryEntry?> GetLatestRollbackReadyAsync(CancellationToken cancellationToken) =>
        ReadAsync(
            "WHERE status = 'completed' AND rollback_version IS NOT NULL ORDER BY started_at DESC LIMIT 1",
            null,
            cancellationToken);

    private Task UpdateAsync(
        long id,
        string status,
        string? rollbackVersion,
        string? error,
        CancellationToken cancellationToken) =>
        archiveWriteQueue.EnqueueAsync(async (connection, transaction, ct) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE update_history
                SET status = @status, completed_at = @completedAt,
                    rollback_version = COALESCE(@rollbackVersion, rollback_version),
                    error_message = @error
                WHERE id = @id;
                """;
            command.Parameters.AddWithValue("@status", status);
            command.Parameters.AddWithValue("@completedAt", clock.UtcNow.ToUnixTimeMilliseconds());
            command.Parameters.AddWithValue("@rollbackVersion", rollbackVersion ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@error", error ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync(ct);
            return true;
        }, cancellationToken);

    private Task<UpdateHistoryEntry?> ReadAsync(
        string predicate,
        long? id,
        CancellationToken cancellationToken) =>
        archiveWriteQueue.EnqueueAsync(async (connection, transaction, ct) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                $"""
                SELECT id, component_type, component_id, from_version, to_version,
                       status, rollback_version, error_message
                FROM update_history
                {predicate};
                """;
            if (id.HasValue)
            {
                command.Parameters.AddWithValue("@id", id.Value);
            }

            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return null;
            }

            return new UpdateHistoryEntry(
                reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetString(4),
                reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7));
        }, cancellationToken);
}
