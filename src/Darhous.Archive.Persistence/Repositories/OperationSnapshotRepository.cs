using System.Data;
using Dapper;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Operations;

namespace Darhous.Archive.Persistence.Repositories;

/// <summary>Write-only-in-practice — snapshots are created alongside the bulk operation they undo, always inside a transaction.</summary>
public sealed class OperationSnapshotRepository(IDbConnection connection, IDbTransaction transaction) : IOperationSnapshotRepository
{
    public async Task<Guid> CreateAsync(Guid? userId, string operationType, string payloadJson, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        var uid = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO operation_snapshots (uid, user_id, operation_type, payload_json, created_at, expires_at)
            VALUES (@Uid, (SELECT id FROM app_users WHERE uid = @UserId), @OperationType, @PayloadJson, @Now, @ExpiresAt);
            """,
            new
            {
                Uid = uid.ToString(), UserId = userId?.ToString(), OperationType = operationType, PayloadJson = payloadJson,
                Now = now, ExpiresAt = expiresAt.ToUnixTimeMilliseconds(),
            },
            transaction,
            cancellationToken: cancellationToken));

        return uid;
    }

    public async Task<OperationSnapshot?> GetByUidAsync(Guid uid, CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT s.uid, u.uid AS user_id, s.operation_type, s.payload_json, s.created_at, s.expires_at, s.reverted_at
            FROM operation_snapshots s
            LEFT JOIN app_users u ON u.id = s.user_id
            WHERE s.uid = @Uid;
            """;

        var row = await connection.QuerySingleOrDefaultAsync<SnapshotRow>(
            new CommandDefinition(sql, new { Uid = uid.ToString() }, transaction, cancellationToken: cancellationToken));

        return row is null
            ? null
            : new OperationSnapshot(
                Guid.Parse(row.Uid), row.UserId is null ? null : Guid.Parse(row.UserId), row.OperationType, row.PayloadJson,
                DateTimeOffset.FromUnixTimeMilliseconds(row.CreatedAt), DateTimeOffset.FromUnixTimeMilliseconds(row.ExpiresAt),
                row.RevertedAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(row.RevertedAt.Value) : null);
    }

    public Task MarkRevertedAsync(Guid uid, DateTimeOffset revertedAt, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            "UPDATE operation_snapshots SET reverted_at = @RevertedAt WHERE uid = @Uid;",
            new { Uid = uid.ToString(), RevertedAt = revertedAt.ToUnixTimeMilliseconds() },
            transaction,
            cancellationToken: cancellationToken));

    private sealed class SnapshotRow
    {
        public string Uid { get; set; } = "";
        public string? UserId { get; set; }
        public string OperationType { get; set; } = "";
        public string PayloadJson { get; set; } = "";
        public long CreatedAt { get; set; }
        public long ExpiresAt { get; set; }
        public long? RevertedAt { get; set; }
    }
}
