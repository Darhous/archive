namespace Darhous.Archive.Contracts.Events;

/// <summary>
/// Plugin SDK §56 (Event Delivery) — Transient is in-memory best-effort, Reliable goes
/// through the Outbox, Critical adds retry + explicit acknowledgement.
/// </summary>
public enum EventDeliveryLevel
{
    Transient,
    Reliable,
    Critical,
}
