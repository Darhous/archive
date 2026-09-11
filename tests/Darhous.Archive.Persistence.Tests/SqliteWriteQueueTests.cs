using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Writes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darhous.Archive.Persistence.Tests;

public class SqliteWriteQueueTests : PersistenceTestBase
{
    private async Task<SqliteWriteQueue> StartQueueAsync()
    {
        var queue = new SqliteWriteQueue(
            DatabaseKind.Archive, new SqliteConnectionFactory(Options), NullLogger<SqliteWriteQueue>.Instance);
        await queue.StartAsync(CancellationToken.None);
        return queue;
    }

    [Fact]
    public async Task EnqueueAsync_ExecutesAndCommits()
    {
        var queue = await StartQueueAsync();
        try
        {
            var roleId = await queue.EnqueueAsync(async (connection, transaction, ct) =>
            {
                await using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT INTO roles (uid, code, display_name, is_system, created_at) " +
                                   "VALUES (@Uid, 'probe', 'Probe', 0, 0); SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@Uid", Guid.NewGuid().ToString());
                return (long)(await cmd.ExecuteScalarAsync(ct))!;
            }, CancellationToken.None);

            Assert.True(roleId > 0);

            // Verify it's actually committed and visible from a separate connection.
            await using var readConnection = await new SqliteConnectionFactory(Options).OpenAsync(DatabaseKind.Archive, CancellationToken.None);
            await using var readCmd = readConnection.CreateCommand();
            readCmd.CommandText = "SELECT COUNT(*) FROM roles WHERE code = 'probe';";
            Assert.Equal(1L, (long)(await readCmd.ExecuteScalarAsync())!);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task EnqueueAsync_OperationThrows_RollsBackAndPropagatesToCaller()
    {
        var queue = await StartQueueAsync();
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                queue.EnqueueAsync<object?>((_, _, _) => throw new InvalidOperationException("boom"), CancellationToken.None));

            // The queue itself must still be usable afterwards (Core Stability Principle).
            var result = await queue.EnqueueAsync((_, _, _) => Task.FromResult(42), CancellationToken.None);
            Assert.Equal(42, result);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task EnqueueAsync_ConcurrentCallers_AreSerialized()
    {
        var queue = await StartQueueAsync();
        try
        {
            var concurrentInsideCritical = 0;
            var maxObservedConcurrency = 0;
            var gate = new object();

            async Task<int> TrackedOperation(int i)
            {
                return await queue.EnqueueAsync(async (connection, transaction, ct) =>
                {
                    lock (gate)
                    {
                        concurrentInsideCritical++;
                        maxObservedConcurrency = Math.Max(maxObservedConcurrency, concurrentInsideCritical);
                    }

                    // Force a yield so a non-serialized implementation would very likely overlap here.
                    await Task.Delay(5, ct);

                    lock (gate)
                    {
                        concurrentInsideCritical--;
                    }

                    return i;
                }, CancellationToken.None);
            }

            var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(TrackedOperation));

            Assert.Equal(Enumerable.Range(0, 20), results.OrderBy(x => x));
            Assert.Equal(1, maxObservedConcurrency);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }
}
