using Darhous.Archive.Contracts.Jobs;

namespace Darhous.Archive.Application.Persistence;

/// <summary>SAD §51-53 (Job System, Crash Recovery). Backs <c>Darhous.Archive.Core.Jobs</c>'s execution-side contracts with the actual `jobs` table.</summary>
public interface IJobRepository
{
    Task<Guid> CreateAsync(NewJob job, CancellationToken cancellationToken);

    Task<Job?> GetByUidAsync(Guid uid, CancellationToken cancellationToken);

    /// <summary>Pending or retrying jobs whose retry delay (if any) has elapsed, highest priority first.</summary>
    Task<IReadOnlyList<Job>> ListReadyToRunAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyList<Job>> ListByStatusAsync(JobStatus status, CancellationToken cancellationToken);

    Task MarkRunningAsync(Guid uid, DateTimeOffset startedAt, CancellationToken cancellationToken);

    Task ReportProgressAsync(Guid uid, int percent, CancellationToken cancellationToken);

    Task MarkSucceededAsync(Guid uid, string? resultJson, DateTimeOffset completedAt, CancellationToken cancellationToken);

    /// <summary>Terminal failure — retries exhausted or the job is not retriable.</summary>
    Task MarkFailedAsync(Guid uid, string errorCode, string errorMessage, DateTimeOffset completedAt, CancellationToken cancellationToken);

    Task ScheduleRetryAsync(Guid uid, int retryCount, DateTimeOffset nextRetryAt, string? errorCode, string? errorMessage, CancellationToken cancellationToken);

    /// <summary>SAD §53 — a job left running across a crash that isn't safe to blindly resume.</summary>
    Task MarkNeedsReviewAsync(Guid uid, string errorCode, string errorMessage, CancellationToken cancellationToken);

    /// <summary>SAD §53 — a job left running across a crash that IS safe to resume: back to pending, started_at cleared.</summary>
    Task RequeueAsync(Guid uid, CancellationToken cancellationToken);
}
