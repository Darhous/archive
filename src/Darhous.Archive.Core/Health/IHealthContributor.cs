using Darhous.Archive.Contracts.Health;

namespace Darhous.Archive.Core.Health;

/// <summary>
/// Host-side counterpart of Plugin SDK's <c>IPluginHealthContributor</c> — every module
/// (DB, Search, Scanner, OCR, Automation, Backup, ...) registers one so the System Health
/// page (SAD §57) can aggregate a single status snapshot.
/// </summary>
public interface IHealthContributor
{
    /// <summary>Stable component name shown on the Health page (e.g. "Database", "Search Index").</summary>
    string Component { get; }

    Task<HealthReport> CheckAsync(CancellationToken cancellationToken);
}
