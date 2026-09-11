using Darhous.Archive.Contracts.Events;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Transactions;
using Darhous.Archive.Persistence.Writes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darhous.Archive.Persistence.Tests;

/// <summary>
/// Phase 2 design review: prove a role insert and an outbox event insert commit or roll
/// back together as one atomic unit, exercising IUnitOfWork -> IUnitOfWorkContext ->
/// IRoleRepository/IOutboxWriter end to end.
/// </summary>
public class UnitOfWorkOutboxTests : PersistenceTestBase
{
    private async Task<(SqliteWriteQueue Queue, SqliteUnitOfWork UnitOfWork)> StartAsync()
    {
        var queue = new SqliteWriteQueue(
            DatabaseKind.Archive, new SqliteConnectionFactory(Options), NullLogger<SqliteWriteQueue>.Instance);
        await queue.StartAsync(CancellationToken.None);
        return (queue, new SqliteUnitOfWork(queue));
    }

    [Fact]
    public async Task ExecuteAsync_InsertsRoleAndOutboxEvent_Atomically()
    {
        var (queue, unitOfWork) = await StartAsync();
        try
        {
            var roleUid = await unitOfWork.ExecuteAsync(async (context, ct) =>
            {
                var uid = await context.Roles.CreateAsync("editor", "Editor", isSystem: false, ct);
                await context.Outbox.EnqueueAsync(
                    "RoleCreated", EventDeliveryLevel.Reliable, new { RoleUid = uid },
                    correlationId: null, userId: null, ct);
                return uid;
            }, CancellationToken.None);

            await using var connection = await new SqliteConnectionFactory(Options).OpenAsync(DatabaseKind.Archive, CancellationToken.None);

            await using var roleCmd = connection.CreateCommand();
            roleCmd.CommandText = "SELECT COUNT(*) FROM roles WHERE uid = @Uid;";
            roleCmd.Parameters.AddWithValue("@Uid", roleUid.ToString());
            Assert.Equal(1L, (long)(await roleCmd.ExecuteScalarAsync())!);

            await using var outboxCmd = connection.CreateCommand();
            outboxCmd.CommandText = "SELECT COUNT(*) FROM outbox_events WHERE event_type = 'RoleCreated' AND delivery_level = 'reliable';";
            Assert.Equal(1L, (long)(await outboxCmd.ExecuteScalarAsync())!);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ExecuteAsync_OutboxWriteFails_RollsBackTheRoleInsertToo()
    {
        var (queue, unitOfWork) = await StartAsync();
        try
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
                {
                    await context.Roles.CreateAsync("should-not-survive", "Should Not Survive", false, ct);

                    // Transient is rejected by OutboxWriter by design — simulates "something
                    // after the business write fails" to prove the whole transaction unwinds.
                    await context.Outbox.EnqueueAsync(
                        "Whatever", EventDeliveryLevel.Transient, new { }, null, null, ct);

                    return null;
                }, CancellationToken.None));

            await using var connection = await new SqliteConnectionFactory(Options).OpenAsync(DatabaseKind.Archive, CancellationToken.None);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM roles WHERE code = 'should-not-survive';";
            Assert.Equal(0L, (long)(await cmd.ExecuteScalarAsync())!);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }
}
