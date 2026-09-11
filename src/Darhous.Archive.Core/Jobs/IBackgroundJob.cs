namespace Darhous.Archive.Core.Jobs;

/// <summary>
/// Any long-running unit of work in the system (OCR, Index, Scan, Import, Backup, Export,
/// Plugin Update — SAD §51). Registered with the Job Manager once it exists (Phase 2).
/// </summary>
public interface IBackgroundJob
{
    /// <summary>Matches <c>JobMetadata.JobType</c> (e.g. "OcrJob", "DiscoveryScanJob").</summary>
    string JobType { get; }

    Task ExecuteAsync(IJobContext context, CancellationToken cancellationToken);
}
