using Darhous.Archive.Modules.Discovery.Exclusions;

namespace Darhous.Archive.Modules.Discovery.Scanning;

public interface IDiscoveryScanner
{
    /// <summary>
    /// Synchronous by design — always invoked from a background <c>IBackgroundJob</c> execution
    /// (never the UI thread), so "runs in Background without freezing the UI" (Implementation
    /// Plan §52) is satisfied structurally by the caller, not by this method being internally async.
    /// </summary>
    ScanResult Scan(string rootPath, bool includeSubfolders, ExclusionSet exclusions, CancellationToken cancellationToken);
}
