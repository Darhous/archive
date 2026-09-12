using System.Collections.Concurrent;
using Darhous.Archive.Contracts.Health;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Core.Health;

/// <summary>
/// Resolves every <see cref="IHealthContributor"/> registered in DI (plus any added later via
/// <see cref="Register"/>, e.g. a dynamically-loaded plugin) and checks them independently —
/// one contributor throwing must not hide the others (SAD §5.1).
/// </summary>
public sealed class HealthRegistry(IEnumerable<IHealthContributor> contributors, ILogger<HealthRegistry> logger)
    : IHealthRegistry
{
    private readonly ConcurrentDictionary<IHealthContributor, byte> _dynamicContributors = new();

    public async Task<IReadOnlyList<HealthReport>> CheckAllAsync(CancellationToken cancellationToken)
    {
        var reports = new List<HealthReport>();

        foreach (var contributor in contributors.Concat(_dynamicContributors.Keys))
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

    public void Register(IHealthContributor contributor) => _dynamicContributors.TryAdd(contributor, 0);

    public void Unregister(IHealthContributor contributor) => _dynamicContributors.TryRemove(contributor, out _);
}
