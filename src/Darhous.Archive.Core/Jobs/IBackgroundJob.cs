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

    /// <summary>
    /// SAD §53 (Crash Recovery): a job the runner finds still marked "running" after a crash —
    /// true means "safe to silently requeue as pending and re-execute from scratch" (the
    /// default; most jobs here are naturally idempotent re-scans/re-indexes), false means
    /// "mark needs_review instead, a human should look at this before it runs again."
    /// </summary>
    bool IsSafeToResumeAfterCrash => true;
}
