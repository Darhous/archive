using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Events;
using Darhous.Archive.Core.Events;

namespace Darhous.Archive.Persistence.Outbox;

/// <summary>
/// Replaces Phase 1's <see cref="InMemoryEventBus"/> as the registered <see cref="IEventBus"/>
/// from Phase 2 onward: Transient still delegates to the in-memory bus unchanged, but
/// Reliable/Critical now actually work — writing to the outbox table via a
/// <see cref="IUnitOfWork"/> transaction of their own, since publishing an event is not
/// itself part of any specific business write (a caller that wants the event to commit
/// atomically with its own change should use <c>context.Outbox.EnqueueAsync</c> directly
/// inside its own <see cref="IUnitOfWork.ExecuteAsync{TResult}"/> call instead of going
/// through this bus).
/// </summary>
public sealed class OutboxEventBus(InMemoryEventBus transientBus, IUnitOfWork unitOfWork) : IEventBus
{
    public async Task PublishAsync<T>(
        string eventType,
        T payload,
        EventDeliveryLevel level,
        Guid? correlationId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        if (level == EventDeliveryLevel.Transient)
        {
            await transientBus.PublishAsync(eventType, payload, level, correlationId, userId, cancellationToken);
            return;
        }

        await unitOfWork.ExecuteAsync<object?>(
            async (context, ct) =>
            {
                await context.Outbox.EnqueueAsync(eventType, level, payload, correlationId, userId, ct);
                return null;
            },
            cancellationToken);
    }

    public IDisposable Subscribe<T>(Func<ArchiveEventEnvelope<T>, CancellationToken, Task> handler) =>
        transientBus.Subscribe(handler);
}
