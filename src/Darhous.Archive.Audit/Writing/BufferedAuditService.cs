using System.Threading.Channels;
using Darhous.Archive.Contracts.Audit;
using Darhous.Archive.Core.Audit;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Writes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Audit.Writing;

/// <summary>
/// Implementation Plan §11 (Audit Project): Critical actions (DB Spec §106) are written
/// immediately through the audit write queue — the caller's <see cref="RecordAsync"/> await
/// doesn't return until the row is committed. Everything else is dropped into a channel and
/// flushed as one batch insert on a timer, trading a few seconds of durability for not
/// serializing every search/sort/filter through the write queue individually.
/// </summary>
public sealed class BufferedAuditService(
    ISqliteWriteQueue writeQueue,
    ISqliteConnectionFactory connectionFactory,
    IClock clock,
    ILogger<BufferedAuditService> logger)
    : BackgroundService, IAuditService, IAuditQueryService
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(2);
    private const int FlushBatchSizeTrigger = 200;

    private readonly Channel<PendingAuditEvent> _buffer = Channel.CreateUnbounded<PendingAuditEvent>();

    public async Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        var pending = new PendingAuditEvent(Guid.CreateVersion7(), clock.UtcNow, entry);

        if (AuditAction.CriticalActions.Contains(entry.Action))
        {
            await writeQueue.EnqueueAsync<object?>(async (connection, transaction, ct) =>
            {
                await AuditEventRepository.InsertBatchAsync(connection, transaction, [pending], ct);
                return null;
            }, cancellationToken);
            return;
        }

        await _buffer.Writer.WriteAsync(pending, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(DatabaseKind.Audit, cancellationToken);
        return await AuditEventRepository.QueryAsync(connection, query, cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pending = new List<PendingAuditEvent>(FlushBatchSizeTrigger);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeoutCts.CancelAfter(FlushInterval);

                try
                {
                    while (pending.Count < FlushBatchSizeTrigger &&
                           await _buffer.Reader.WaitToReadAsync(timeoutCts.Token))
                    {
                        while (pending.Count < FlushBatchSizeTrigger && _buffer.Reader.TryRead(out var item))
                        {
                            pending.Add(item);
                        }
                    }
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // Flush-interval timeout, not shutdown — fall through and flush whatever we have.
                }

                if (pending.Count > 0)
                {
                    await FlushAsync(pending, stoppingToken);
                    pending.Clear();
                }
            }
        }
        finally
        {
            // Drain anything left in the channel on shutdown rather than losing it.
            while (_buffer.Reader.TryRead(out var item))
            {
                pending.Add(item);
            }

            if (pending.Count > 0)
            {
                await FlushAsync(pending, CancellationToken.None);
            }
        }
    }

    private async Task FlushAsync(IReadOnlyCollection<PendingAuditEvent> batch, CancellationToken cancellationToken)
    {
        try
        {
            await writeQueue.EnqueueAsync<object?>(async (connection, transaction, ct) =>
            {
                await AuditEventRepository.InsertBatchAsync(connection, transaction, batch, ct);
                return null;
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            // Losing buffered (non-critical) audit rows on a transient failure is acceptable —
            // losing the ability to keep auditing anything is not (SAD §5.1 Core Stability).
            logger.LogError(ex, "Failed to flush {Count} buffered audit events", batch.Count);
        }
    }
}
