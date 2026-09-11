using Darhous.Archive.Contracts.Events;

namespace Darhous.Archive.Core.Events;

/// <summary>
/// SAD §54 (Event Bus) — modules/plugins talk through this instead of holding direct
/// references to each other (e.g. Scanner never knows Telegram exists).
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes <paramref name="payload"/> at the given delivery level (Plugin SDK §56).
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// Thrown for <see cref="EventDeliveryLevel.Reliable"/>/<see cref="EventDeliveryLevel.Critical"/>
    /// until the Outbox-backed implementation lands with Persistence (Phase 2, DB Spec §57).
    /// </exception>
    Task PublishAsync<T>(
        string eventType,
        T payload,
        EventDeliveryLevel level,
        Guid? correlationId,
        Guid? userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Subscribes to every envelope published for <typeparamref name="T"/>.
    /// Dispose the returned handle to unsubscribe.
    /// </summary>
    IDisposable Subscribe<T>(Func<ArchiveEventEnvelope<T>, CancellationToken, Task> handler);
}
