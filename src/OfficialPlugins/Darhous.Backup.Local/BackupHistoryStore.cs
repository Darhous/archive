using Microsoft.Data.Sqlite;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Persistence.Writes;

namespace Darhous.Backup.Local;

internal sealed class BackupHistoryStore(ISqliteWriteQueue archiveWriteQueue, IClock clock)
{
    public Task StartAsync(Guid uid, BackupRequest request, CancellationToken cancellationToken) =>
        archiveWriteQueue.EnqueueAsync(async (connection, transaction, ct) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO backup_history
                    (uid, backup_type, destination, status, created_by, started_at)
                VALUES
                    (@uid, @type, @destination, 'running',
                     (SELECT id FROM app_users WHERE uid = @createdBy), @startedAt);
                """;
            command.Parameters.AddWithValue("@uid", uid.ToString());
            command.Parameters.AddWithValue("@type", request.Type.ToStorageName());
            command.Parameters.AddWithValue("@destination", Path.GetFullPath(request.DestinationDirectory));
            command.Parameters.AddWithValue("@createdBy", request.RequestedBy?.ToString() ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@startedAt", clock.UtcNow.ToUnixTimeMilliseconds());
            await command.ExecuteNonQueryAsync(ct);
            return true;
        }, cancellationToken);

    public Task CompleteAsync(Guid uid, BackupResult result, CancellationToken cancellationToken) =>
        UpdateAsync(uid, "completed", Path.GetFileName(result.PackagePath), result.FileSize, result.Sha256, null, cancellationToken);

    public Task FailAsync(Guid uid, Exception exception, CancellationToken cancellationToken) =>
        UpdateAsync(uid, "failed", null, null, null, exception.Message, cancellationToken);

    private Task UpdateAsync(
        Guid uid,
        string status,
        string? fileName,
        long? fileSize,
        string? checksum,
        string? errorMessage,
        CancellationToken cancellationToken) =>
        archiveWriteQueue.EnqueueAsync(async (connection, transaction, ct) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE backup_history
                SET status = @status, file_name = @fileName, file_size = @fileSize,
                    checksum = @checksum, completed_at = @completedAt, error_message = @error
                WHERE uid = @uid;
                """;
            command.Parameters.AddWithValue("@status", status);
            command.Parameters.AddWithValue("@fileName", fileName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@fileSize", fileSize ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@checksum", checksum ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@completedAt", clock.UtcNow.ToUnixTimeMilliseconds());
            command.Parameters.AddWithValue("@error", errorMessage ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@uid", uid.ToString());
            await command.ExecuteNonQueryAsync(ct);
            return true;
        }, cancellationToken);
}
