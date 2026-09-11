using Darhous.Archive.Contracts.Events;

namespace Darhous.Archive.Application.Persistence;

/// <summary>
/// Appends a Reliable/Critical event to the outbox as part of the ambient <see cref="IUnitOfWork"/>
/// transaction — never on its own connection, so the business write and the outbox row commit
/// or roll back together (DB Spec §121 Outbox Consistency).
/// </summary>
public interface IOutboxWriter
{
    Task EnqueueAsync<T>(
        string eventType,
        EventDeliveryLevel level,
        T payload,
        Guid? correlationId,
        Guid? userId,
        CancellationToken cancellationToken);
}
