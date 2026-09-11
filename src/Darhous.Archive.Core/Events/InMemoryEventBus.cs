using System.Collections.Concurrent;
using System.Threading;
using Darhous.Archive.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Core.Events;

/// <summary>
/// Transient-tier <see cref="IEventBus"/>: in-process, best-effort, no persistence.
/// Reliable/Critical delivery (Outbox-backed) is added by Persistence in Phase 2 —
/// calling <see cref="PublishAsync{T}"/> with those levels fails loudly rather than
/// silently downgrading to Transient.
/// </summary>
public sealed class InMemoryEventBus(ILogger<InMemoryEventBus> logger) : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Subscription>> _subscriptions = new();
    private readonly Lock _gate = new();

    public async Task PublishAsync<T>(
        string eventType,
        T payload,
        EventDeliveryLevel level,
        Guid? correlationId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        if (level != EventDeliveryLevel.Transient)
        {
            throw new NotSupportedException(
                $"{level} event delivery requires the Outbox (Persistence, Phase 2) and is not available yet. " +
                $"Event '{eventType}' must be published as Transient until then.");
        }

        var envelope = new ArchiveEventEnvelope<T>(
            Guid.NewGuid(), DateTimeOffset.UtcNow, eventType, payload, correlationId, userId);

        if (!_subscriptions.TryGetValue(typeof(T), out var subscribers))
        {
            return;
        }

        Subscription[] snapshot;
        lock (_gate)
        {
            snapshot = [.. subscribers];
        }

        foreach (var subscription in snapshot)
        {
            try
            {
                await ((Func<ArchiveEventEnvelope<T>, CancellationToken, Task>)subscription.Handler)(envelope, cancellationToken);
            }
            catch (Exception ex)
            {
                // A subscriber failure must never break the publisher or other subscribers
                // (SAD §5.1 Core Stability Principle).
                logger.LogError(ex, "Event subscriber failed for {EventType}", eventType);
            }
        }
    }

    public IDisposable Subscribe<T>(Func<ArchiveEventEnvelope<T>, CancellationToken, Task> handler)
    {
        var subscription = new Subscription(handler);
        var list = _subscriptions.GetOrAdd(typeof(T), static _ => []);

        lock (_gate)
        {
            list.Add(subscription);
        }

        return new Unsubscriber(() =>
        {
            lock (_gate)
            {
                list.Remove(subscription);
            }
        });
    }

    private sealed record Subscription(Delegate Handler);

    private sealed class Unsubscriber(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
