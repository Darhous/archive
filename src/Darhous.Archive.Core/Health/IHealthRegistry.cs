using Darhous.Archive.Contracts.Health;

namespace Darhous.Archive.Core.Health;

/// <summary>
/// Aggregates every registered <see cref="IHealthContributor"/> into one snapshot for the
/// System Health page (SAD §57) and its actions (Retry, Restart worker, Rebuild index...).
/// </summary>
public interface IHealthRegistry
{
    Task<IReadOnlyList<HealthReport>> CheckAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Adds a contributor discovered after the DI container was built — the only case this
    /// covers today is a dynamically-loaded plugin (Plugin SDK §71 <c>IPluginHealthContributor</c>
    /// folds into this same registry rather than a parallel plugin-only health system).
    /// Every contributor wired at DI-container-build time is already included automatically.
    /// </summary>
    void Register(IHealthContributor contributor);

    /// <summary>Removes a dynamically-added contributor — called when its owning plugin stops/uninstalls.</summary>
    void Unregister(IHealthContributor contributor);
}
