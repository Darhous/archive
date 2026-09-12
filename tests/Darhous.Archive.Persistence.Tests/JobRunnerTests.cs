using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Jobs;
using Darhous.Archive.Core.Jobs;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Jobs;
using Darhous.Archive.Persistence.Transactions;
using Darhous.Archive.Persistence.Writes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darhous.Archive.Persistence.Tests;

public class JobRunnerTests : PersistenceTestBase
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    private sealed class RecordingJob(string jobType, Func<Task> onExecute, bool isSafeToResumeAfterCrash = true) : IBackgroundJob
    {
        public int ExecutionCount { get; private set; }
        public string JobType { get; } = jobType;
        public bool IsSafeToResumeAfterCrash { get; } = isSafeToResumeAfterCrash;

        public async Task ExecuteAsync(IJobContext context, CancellationToken cancellationToken)
        {
            ExecutionCount++;
            await onExecute();
        }
    }

    private async Task<SqliteWriteQueue> StartQueueAsync()
    {
        var queue = new SqliteWriteQueue(DatabaseKind.Archive, new SqliteConnectionFactory(Options), NullLogger<SqliteWriteQueue>.Instance);
        await queue.StartAsync(CancellationToken.None);
        return queue;
    }

    private static JobRunner MakeRunner(SqliteUnitOfWork unitOfWork, FakeClock clock, JobRunnerOptions options, params IBackgroundJob[] jobs) =>
        new(unitOfWork, jobs, clock, options, NullLogger<JobRunner>.Instance);

    [Fact]
    public async Task RunOnceAsync_ExecutesReadyJob_MarksSucceeded()
    {
        var queue = await StartQueueAsync();
        try
        {
            var unitOfWork = new SqliteUnitOfWork(queue);
            var clock = new FakeClock();
            var job = new RecordingJob("TestJob", () => Task.CompletedTask);
            var runner = MakeRunner(unitOfWork, clock, new JobRunnerOptions(), job);

            var uid = await unitOfWork.ExecuteAsync(
                (context, ct) => context.Jobs.CreateAsync(new NewJob("TestJob", "Tests", null, null, null, MaxRetries: 0, Priority: 0, null), ct),
                CancellationToken.None);

            await runner.RunOnceAsync(CancellationToken.None);

            var persisted = await unitOfWork.ExecuteAsync((context, ct) => context.Jobs.GetByUidAsync(uid, ct), CancellationToken.None);
            Assert.Equal(1, job.ExecutionCount);
            Assert.Equal(JobStatus.Succeeded, persisted!.Status);
            Assert.Equal(100, persisted.ProgressPercent);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task RunOnceAsync_JobThrowsWithRetriesRemaining_SchedulesRetryInTheFuture()
    {
        var queue = await StartQueueAsync();
        try
        {
            var unitOfWork = new SqliteUnitOfWork(queue);
            var clock = new FakeClock();
            var job = new RecordingJob("FlakyJob", () => throw new InvalidOperationException("boom"));
            var runner = MakeRunner(unitOfWork, clock, new JobRunnerOptions { BaseRetryDelay = TimeSpan.FromMinutes(1) }, job);

            var uid = await unitOfWork.ExecuteAsync(
                (context, ct) => context.Jobs.CreateAsync(new NewJob("FlakyJob", "Tests", null, null, null, MaxRetries: 3, Priority: 0, null), ct),
                CancellationToken.None);

            await runner.RunOnceAsync(CancellationToken.None);

            var persisted = await unitOfWork.ExecuteAsync((context, ct) => context.Jobs.GetByUidAsync(uid, ct), CancellationToken.None);
            Assert.Equal(JobStatus.Retrying, persisted!.Status);
            Assert.Equal(1, persisted.RetryCount);
            Assert.True(persisted.NextRetryAt > clock.UtcNow);

            // Not due yet — a second pass right now must not re-execute it.
            await runner.RunOnceAsync(CancellationToken.None);
            Assert.Equal(1, job.ExecutionCount);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task RunOnceAsync_JobThrowsBeyondMaxRetries_MarksFailed()
    {
        var queue = await StartQueueAsync();
        try
        {
            var unitOfWork = new SqliteUnitOfWork(queue);
            var clock = new FakeClock();
            var job = new RecordingJob("AlwaysFailsJob", () => throw new InvalidOperationException("boom"));
            var runner = MakeRunner(unitOfWork, clock, new JobRunnerOptions(), job);

            var uid = await unitOfWork.ExecuteAsync(
                (context, ct) => context.Jobs.CreateAsync(new NewJob("AlwaysFailsJob", "Tests", null, null, null, MaxRetries: 0, Priority: 0, null), ct),
                CancellationToken.None);

            await runner.RunOnceAsync(CancellationToken.None);

            var persisted = await unitOfWork.ExecuteAsync((context, ct) => context.Jobs.GetByUidAsync(uid, ct), CancellationToken.None);
            Assert.Equal(JobStatus.Failed, persisted!.Status);
            Assert.Equal("JOB_FAILED", persisted.ErrorCode);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task RunOnceAsync_RespectsPriorityOrdering()
    {
        var queue = await StartQueueAsync();
        try
        {
            var unitOfWork = new SqliteUnitOfWork(queue);
            var clock = new FakeClock();

            await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
            {
                await context.Jobs.CreateAsync(new NewJob("OrderedJob", "Tests", null, null, "low", MaxRetries: 0, Priority: 0, null), ct);
                await context.Jobs.CreateAsync(new NewJob("OrderedJob", "Tests", null, null, "high", MaxRetries: 0, Priority: 10, null), ct);
                return null;
            }, CancellationToken.None);

            var ready = await unitOfWork.ExecuteAsync((context, ct) => context.Jobs.ListReadyToRunAsync(clock.UtcNow, 10, ct), CancellationToken.None);

            Assert.Equal(2, ready.Count);
            Assert.Equal("high", ready[0].PayloadJson);
            Assert.Equal("low", ready[1].PayloadJson);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task RecoverFromCrashAsync_SafeJob_RequeuesToPending()
    {
        var queue = await StartQueueAsync();
        try
        {
            var unitOfWork = new SqliteUnitOfWork(queue);
            var clock = new FakeClock();
            var job = new RecordingJob("ResumableJob", () => Task.CompletedTask, isSafeToResumeAfterCrash: true);
            var runner = MakeRunner(unitOfWork, clock, new JobRunnerOptions(), job);

            var uid = await unitOfWork.ExecuteAsync(
                (context, ct) => context.Jobs.CreateAsync(new NewJob("ResumableJob", "Tests", null, null, null, MaxRetries: 0, Priority: 0, null), ct),
                CancellationToken.None);
            await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
            {
                await context.Jobs.MarkRunningAsync(uid, clock.UtcNow, ct);
                return null;
            }, CancellationToken.None);

            // Simulate a fresh process (new runner instance) discovering an orphaned "running" job.
            await runner.RecoverFromCrashAsync(CancellationToken.None);

            var persisted = await unitOfWork.ExecuteAsync((context, ct) => context.Jobs.GetByUidAsync(uid, ct), CancellationToken.None);
            Assert.Equal(JobStatus.Pending, persisted!.Status);
            Assert.Null(persisted.StartedAt);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task RecoverFromCrashAsync_UnsafeJob_MarksNeedsReview()
    {
        var queue = await StartQueueAsync();
        try
        {
            var unitOfWork = new SqliteUnitOfWork(queue);
            var clock = new FakeClock();
            var job = new RecordingJob("UnsafeJob", () => Task.CompletedTask, isSafeToResumeAfterCrash: false);
            var runner = MakeRunner(unitOfWork, clock, new JobRunnerOptions(), job);

            var uid = await unitOfWork.ExecuteAsync(
                (context, ct) => context.Jobs.CreateAsync(new NewJob("UnsafeJob", "Tests", null, null, null, MaxRetries: 0, Priority: 0, null), ct),
                CancellationToken.None);
            await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
            {
                await context.Jobs.MarkRunningAsync(uid, clock.UtcNow, ct);
                return null;
            }, CancellationToken.None);

            await runner.RecoverFromCrashAsync(CancellationToken.None);

            var persisted = await unitOfWork.ExecuteAsync((context, ct) => context.Jobs.GetByUidAsync(uid, ct), CancellationToken.None);
            Assert.Equal(JobStatus.NeedsReview, persisted!.Status);
        }
        finally
        {
            await queue.StopAsync(CancellationToken.None);
        }
    }
}
