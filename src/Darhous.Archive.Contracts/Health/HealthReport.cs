namespace Darhous.Archive.Contracts.Health;

/// <summary>
/// The result of one component's health check (feeds the System Health page — SAD §57).
/// </summary>
public sealed record HealthReport(
    string Component,
    HealthStatus Status,
    string? Description,
    DateTimeOffset CheckedAt,
    IReadOnlyDictionary<string, string>? Data = null)
{
    public static HealthReport Healthy(string component, string? description = null, DateTimeOffset? checkedAt = null) =>
        new(component, HealthStatus.Healthy, description, checkedAt ?? DateTimeOffset.UtcNow);

    public static HealthReport Failed(string component, string description, DateTimeOffset? checkedAt = null) =>
        new(component, HealthStatus.Failed, description, checkedAt ?? DateTimeOffset.UtcNow);
}
