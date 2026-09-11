using Darhous.Archive.Contracts.Jobs;

namespace Darhous.Archive.Core.Jobs;

/// <summary>
/// What a running <see cref="IBackgroundJob"/> gets to report progress and read its own metadata.
/// The actual scheduler/persistence for jobs (the `jobs` table, retry policy, crash recovery —
/// SAD §51-53) is built with Persistence in Phase 2; this is only the execution-side contract.
/// </summary>
public interface IJobContext
{
    JobMetadata Metadata { get; }

    Task ReportProgressAsync(int percent, CancellationToken cancellationToken);
}
