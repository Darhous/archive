using System.Threading.Channels;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;

namespace Darhous.Archive.Persistence.Writes;

/// <summary>
/// One persistent writable connection + one background consumer loop per database
/// (started/stopped with the host). Producers get a <see cref="Task{TResult}"/> back
/// immediately from <see cref="EnqueueAsync{TResult}"/>; the actual work runs serialized,
/// one transaction per queued item, on the single consumer.
///
/// Note on isolation: a plain <c>BeginTransactionAsync</c> (SQLite's default deferred BEGIN)
/// is used rather than raw "BEGIN IMMEDIATE" SQL — with exactly one writer connection ever
/// open per database (this class), there is no internal writer-vs-writer race to upgrade
/// from; <c>busy_timeout</c> (Configuration.PersistenceOptions) remains the defense-in-depth
/// against an external process (a DB browser, a backup tool) holding a conflicting lock.
/// </summary>
public sealed class SqliteWriteQueue(
    DatabaseKind database,
    ISqliteConnectionFactory connectionFactory,
    ILogger<SqliteWriteQueue> logger)
    : BackgroundService, ISqliteWriteQueue
{
    private readonly Channel<Func<CancellationToken, Task>> _channel =
        Channel.CreateBounded<Func<CancellationToken, Task>>(new BoundedChannelOptions(1024)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });

    private SqliteConnection? _writerConnection;

    public async Task<TResult> EnqueueAsync<TResult>(
        Func<SqliteConnection, SqliteTransaction, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task RunOnWriterAsync(CancellationToken workerToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
                return;
            }

            var connection = _writerConnection
                ?? throw new InvalidOperationException($"Write queue for {database} has not started yet.");

            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(workerToken);
            try
            {
                var result = await operation(connection, transaction, workerToken);
                await transaction.CommitAsync(workerToken);
                completion.TrySetResult(result);
            }
            catch (Exception ex)
            {
                try
                {
                    await transaction.RollbackAsync(workerToken);
                }
                catch (Exception rollbackEx)
                {
                    logger.LogError(rollbackEx, "Rollback itself failed for {Database} after a write error", database);
                }

                completion.TrySetException(ex);
            }
        }

        if (!await _channel.Writer.WaitToWriteAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Write queue for {database} is shut down.");
        }

        await _channel.Writer.WriteAsync(RunOnWriterAsync, cancellationToken);
        return await completion.Task;
    }

    public async Task<ISqliteMaintenanceLease> PauseAsync(CancellationToken cancellationToken)
    {
        var acquired = new TaskCompletionSource<ISqliteMaintenanceLease>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task PauseConsumerAsync(CancellationToken workerToken)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    acquired.TrySetCanceled(cancellationToken);
                    return;
                }

                var connection = _writerConnection
                    ?? throw new InvalidOperationException($"Write queue for {database} has not started yet.");
                var lease = new MaintenanceLease(connection, resume);
                acquired.TrySetResult(lease);

                try
                {
                    await resume.Task.WaitAsync(workerToken);
                }
                catch (OperationCanceledException) when (workerToken.IsCancellationRequested)
                {
                    resume.TrySetResult();
                }
            }
            catch (Exception exception)
            {
                acquired.TrySetException(exception);
            }
        }

        if (!await _channel.Writer.WaitToWriteAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Write queue for {database} is shut down.");
        }

        await _channel.Writer.WriteAsync(PauseConsumerAsync, cancellationToken);
        // Do not apply a second cancellation race here: once the consumer has published the
        // lease, abandoning this await could orphan a live pause that nobody can dispose.
        // Cancellation requested while the item is still queued is observed inside the item.
        return await acquired.Task;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _writerConnection = await connectionFactory.OpenAsync(database, stoppingToken);

        try
        {
            await foreach (var workItem in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await workItem(stoppingToken);
                }
                catch (Exception ex)
                {
                    // The work item itself already routes its exception to the caller's
                    // TaskCompletionSource; this only guards the consumer loop from dying
                    // (SAD §5.1 Core Stability Principle — one bad write must not take
                    // down every future write for this database).
                    logger.LogError(ex, "Unhandled error while processing a queued write for {Database}", database);
                }
            }
        }
        finally
        {
            _channel.Writer.TryComplete();
            if (_writerConnection is not null)
            {
                await _writerConnection.DisposeAsync();
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }

    private sealed class MaintenanceLease(
        SqliteConnection connection,
        TaskCompletionSource resume) : ISqliteMaintenanceLease
    {
        private int _disposed;

        public async Task<TResult> ExecuteAsync<TResult>(
            Func<SqliteConnection, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            return await operation(connection, cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                resume.TrySetResult();
            }

            return ValueTask.CompletedTask;
        }
    }
}
