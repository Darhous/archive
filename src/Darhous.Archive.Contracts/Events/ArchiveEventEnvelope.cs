namespace Darhous.Archive.Contracts.Events;

/// <summary>
/// Plugin SDK §55 (Event Envelope) — the wrapper every published event travels in,
/// whether delivered in-memory (Transient) or through the Outbox (Reliable/Critical).
/// </summary>
public sealed record ArchiveEventEnvelope<T>(
    Guid EventId,
    DateTimeOffset Timestamp,
    string EventType,
    T Payload,
    Guid? CorrelationId,
    Guid? UserId);
