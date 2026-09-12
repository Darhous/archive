using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Jobs;
using Darhous.Archive.Core.Jobs;
using Darhous.Archive.Core.Time;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Persistence.Jobs;

/// <summary>
/// SAD §51-53 (Job System, Crash Recovery). One job at a time, single-instance runner (V1
/// simplification — see <see cref="JobRunnerOptions.BatchSize"/>). Every status transition is
/// its own <see cref="IUnitOfWork.ExecuteAsync{TResult}"/> call rather than one long-held
/// transaction spanning the job's actual work, since job execution can take arbitrarily long
/// (a full-disk Discovery scan) and must never hold the archive.db write queue's single
/// connection hostage for that whole duration.
/// </summary>
public sealed class JobRunner(
    IUnitOfWork unitOfWork, IEnumerable<IBackgroundJob> registeredJobs, IClock clock,
    JobRunnerOptions options, ILogger<JobRunner> logger)
    : BackgroundService
{
    private readonly Dictionary<string, IBackgroundJob> _jobsByType = registeredJobs.ToDictionary(j => j.JobType);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverFromCrashAsync(stoppingToken);

        using var timer = new PeriodicTimer(options.PollInterval);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One bad poll must not kill the runner (SAD §5.1) — the next tick tries again.
                logger.LogError(ex, "Job runner poll failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Public for direct invocation from tests without waiting on the timer.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var ready = await unitOfWork.ExecuteAsync(
            (context, ct) => context.Jobs.ListReadyToRunAsync(clock.UtcNow, options.BatchSize, ct), cancellationToken);

        foreach (var job in ready)
        {
            await ExecuteJobAsync(job, cancellationToken);
        }
    }

    private async Task ExecuteJobAsync(Job job, CancellationToken cancellationToken)
    {
        if (!_jobsByType.TryGetValue(job.JobType, out var handler))
        {
            logger.LogError("No IBackgroundJob registered for job type {JobType} (job {JobUid}) — marking needs_review", job.JobType, job.Uid);
            await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
            {
                await context.Jobs.MarkNeedsReviewAsync(job.Uid, "JOB_TYPE_NOT_REGISTERED", $"No handler registered for job type '{job.JobType}'.", ct);
                return null;
            }, cancellationToken);
            return;
        }

        var startedAt = clock.UtcNow;
        await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.Jobs.MarkRunningAsync(job.Uid, startedAt, ct);
            return null;
        }, cancellationToken);

        var metadata = new JobMetadata(
            job.Uid, job.PluginId, job.JobType, JobStatus.Running, 0, job.UserId,
            job.CreatedAt, startedAt, null, job.RetryCount, null, null, job.CorrelationId, job.PayloadJson);
        var context = new PersistedJobContext(metadata, unitOfWork);

        try
        {
            await handler.ExecuteAsync(context, cancellationToken);

            await unitOfWork.ExecuteAsync<object?>(async (uowContext, ct) =>
            {
                await uowContext.Jobs.MarkSucceededAsync(job.Uid, resultJson: null, clock.UtcNow, ct);
                return null;
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await HandleJobFailureAsync(job, ex, cancellationToken);
        }
    }

    private async Task HandleJobFailureAsync(Job job, Exception ex, CancellationToken cancellationToken)
    {
        var nextRetryCount = job.RetryCount + 1;
        logger.LogError(ex, "Job {JobUid} ({JobType}) failed on attempt {Attempt}", job.Uid, job.JobType, nextRetryCount);

        if (nextRetryCount > job.MaxRetries)
        {
            await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
            {
                await context.Jobs.MarkFailedAsync(job.Uid, "JOB_FAILED", ex.Message, clock.UtcNow, ct);
                return null;
            }, cancellationToken);
            return;
        }

        var backoff = TimeSpan.FromTicks(Math.Min(
            options.BaseRetryDelay.Ticks * (1L << Math.Min(nextRetryCount - 1, 20)),
            options.MaxRetryDelay.Ticks));

        await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.Jobs.ScheduleRetryAsync(job.Uid, nextRetryCount, clock.UtcNow.Add(backoff), "JOB_FAILED", ex.Message, ct);
            return null;
        }, cancellationToken);
    }

    /// <summary>
    /// A job left "running" means the app died mid-execution — SAD §53 requires deciding per
    /// job whether that's safe to silently redo (<see cref="IBackgroundJob.IsSafeToResumeAfterCrash"/>)
    /// or needs a human to look at it first.
    /// </summary>
    /// <summary>Public for direct invocation from tests without waiting on host startup.</summary>
    public async Task RecoverFromCrashAsync(CancellationToken cancellationToken)
    {
        var orphaned = await unitOfWork.ExecuteAsync(
            (context, ct) => context.Jobs.ListByStatusAsync(JobStatus.Running, ct), cancellationToken);

        foreach (var job in orphaned)
        {
            var safeToResume = !_jobsByType.TryGetValue(job.JobType, out var handler) || handler.IsSafeToResumeAfterCrash;

            await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
            {
                if (safeToResume)
                {
                    await context.Jobs.RequeueAsync(job.Uid, ct);
                }
                else
                {
                    await context.Jobs.MarkNeedsReviewAsync(job.Uid, "JOB_INTERRUPTED", "Job was still running when the app last shut down.", ct);
                }

                return null;
            }, cancellationToken);

            logger.LogWarning(
                "Recovered orphaned job {JobUid} ({JobType}) from a previous run — {Action}",
                job.Uid, job.JobType, safeToResume ? "requeued" : "marked needs_review");
        }
    }
}
