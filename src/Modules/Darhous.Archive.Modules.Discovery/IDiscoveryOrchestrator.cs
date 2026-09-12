using Darhous.Archive.Core.Results;

namespace Darhous.Archive.Modules.Discovery;

/// <summary>Entry points the Onboarding UI (and, later, a "rescan now" action) call — everything downstream runs as background Jobs (Implementation Plan §53).</summary>
public interface IDiscoveryOrchestrator
{
    /// <summary>SAD §46.9 default: scans only the user's chosen watch folders. Fails if none are configured.</summary>
    Task<Result<Guid>> StartInitialDiscoveryAsync(Guid? requestedBy, CancellationToken cancellationToken);

    /// <summary>SAD §46.9's explicit opt-in: scans every enabled fixed drive (Implementation Plan §46.1-46.8, unchanged).</summary>
    Task<Result<Guid>> StartFullComputerScanAsync(Guid? requestedBy, CancellationToken cancellationToken);
}
