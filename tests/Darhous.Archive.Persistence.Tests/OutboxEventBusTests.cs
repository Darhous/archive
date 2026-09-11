using Darhous.Archive.Contracts.Events;
using Darhous.Archive.Core.Events;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Outbox;
using Darhous.Archive.Persistence.Transactions;
using Darhous.Archive.Persistence.Writes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darhous.Archive.Persistence.Tests;

public class OutboxEventBusTests : PersistenceTestBase
{
    private async Task<(SqliteWriteQueue Queue, OutboxEventBus Bus)> StartAsync()
    {
        var queue = new SqliteWriteQueue(
            DatabaseKind.Archive, new SqliteConnectionFactory(Options), NullLogger<SqliteWriteQueue>.Instance);
        await queue.StartAsync(CancellationToken.None);

        var transientBus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);
        var unitOfWork = new SqliteUnitOfWork(queue);
        return (queue, new OutboxEventBus(transientBus, unitOfWork));
    }

    [Fact]
    public async Task PublishAsync_Transient_StillDeliversInMemoryOnly_NoOutboxRow()
    {
        var (queue, bus) = await StartAsync();
        try
        {
            var received = false;
            using var subscription = bus.Subscribe<string>((_, _) =>
            {
                received = true;
                return Task.CompletedTask;
            });

            await bus.PublishAsync("Ping", "payload", EventDeliveryLevel.Transient, null, null, CancellationToken.None);

            Assert.True(received);

            await using var connection = await new SqliteConnectionFactory(Options).OpenAsync(DatabaseKind.Archive, CancellationToken.None);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM outbox_events;";
            Assert.Equal(0L, (long)(await cmd.ExecuteScalarAsync())!);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task PublishAsync_Reliable_WritesOutboxRow_UnlikePhase1WhichThrew()
    {
        var (queue, bus) = await StartAsync();
        try
        {
            await bus.PublishAsync("BackupCompleted", new { Ok = true }, EventDeliveryLevel.Reliable, null, null, CancellationToken.None);

            await using var connection = await new SqliteConnectionFactory(Options).OpenAsync(DatabaseKind.Archive, CancellationToken.None);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT status, delivery_level FROM outbox_events WHERE event_type = 'BackupCompleted';";
            await using var reader = await cmd.ExecuteReaderAsync();

            Assert.True(await reader.ReadAsync());
            Assert.Equal("pending", reader.GetString(0));
            Assert.Equal("reliable", reader.GetString(1));
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task PublishAsync_Critical_WritesOutboxRowWithCriticalLevel()
    {
        var (queue, bus) = await StartAsync();
        try
        {
            await bus.PublishAsync("PluginFailed", new { PluginId = "x" }, EventDeliveryLevel.Critical, null, null, CancellationToken.None);

            await using var connection = await new SqliteConnectionFactory(Options).OpenAsync(DatabaseKind.Archive, CancellationToken.None);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT delivery_level FROM outbox_events WHERE event_type = 'PluginFailed';";
            Assert.Equal("critical", (string)(await cmd.ExecuteScalarAsync())!);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }
}
