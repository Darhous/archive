namespace Darhous.Archive.Contracts.Events;

/// <summary>
/// DB Spec (outbox_events table, `archive.db`) — the durable row backing Reliable/Critical
/// <see cref="EventDeliveryLevel"/> delivery. Only Reliable and Critical ever produce a row
/// here; Transient events never touch the database.
/// </summary>
public sealed record OutboxEvent(
    Guid Uid,
    string EventType,
    EventDeliveryLevel DeliveryLevel,
    string PayloadJson,
    Guid? CorrelationId,
    Guid? UserId,
    OutboxStatus Status,
    int RetryCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt,
    DateTimeOffset? NextRetryAt,
    string? LastError);

/// <summary>DB Spec §65 (Outbox Status).</summary>
public enum OutboxStatus
{
    Pending,
    Processing,
    Processed,
    Failed,
    DeadLetter,
}
