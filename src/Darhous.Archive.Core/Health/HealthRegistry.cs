using Darhous.Archive.Contracts.Health;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Core.Health;

/// <summary>
/// Resolves every <see cref="IHealthContributor"/> registered in DI and checks them
/// independently — one contributor throwing must not hide the others (SAD §5.1).
/// </summary>
public sealed class HealthRegistry(IEnumerable<IHealthContributor> contributors, ILogger<HealthRegistry> logger)
    : IHealthRegistry
{
    public async Task<IReadOnlyList<HealthReport>> CheckAllAsync(CancellationToken cancellationToken)
    {
        var reports = new List<HealthReport>();

        foreach (var contributor in contributors)
        {
            try
            {
                reports.Add(await contributor.CheckAsync(cancellationToken));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Health check failed for {Component}", contributor.Component);
                reports.Add(HealthReport.Failed(contributor.Component, ex.Message));
            }
        }

        return reports;
    }
}
