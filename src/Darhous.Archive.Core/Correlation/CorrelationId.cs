namespace Darhous.Archive.Core.Correlation;

/// <summary>
/// Implementation Plan §31 — every Command/Job/Event/Worker request/Plugin request
/// carries one of these so a single user action can be traced end to end in logs/audit.
/// </summary>
public readonly record struct CorrelationId(Guid Value)
{
    public static CorrelationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
