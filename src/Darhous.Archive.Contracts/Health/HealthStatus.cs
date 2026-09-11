namespace Darhous.Archive.Contracts.Health;

/// <summary>
/// Plugin SDK §72 (Health States) — also used for host-level components (SAD §57 System Health).
/// </summary>
public enum HealthStatus
{
    Unknown,
    Healthy,
    Degraded,
    Failed,
    Disabled,
}
