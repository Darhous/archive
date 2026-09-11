using Darhous.Archive.Contracts.Health;

namespace Darhous.Archive.Core.Health;

/// <summary>
/// Aggregates every registered <see cref="IHealthContributor"/> into one snapshot for the
/// System Health page (SAD §57) and its actions (Retry, Restart worker, Rebuild index...).
/// </summary>
public interface IHealthRegistry
{
    Task<IReadOnlyList<HealthReport>> CheckAllAsync(CancellationToken cancellationToken);
}
