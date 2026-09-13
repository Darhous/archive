using Darhous.Backup.Local.Jobs;
using Darhous.Archive.Contracts.Jobs;
using Darhous.Archive.Core.Jobs;
using System.Text.Json;

namespace Darhous.Backup.Local.Tests;

public sealed class PluginTests
{
    [Fact]
    public void OfficialPlugin_UsesRequiredIdentityAndBackupJobIsNotCrashResumable()
    {
        var plugin = new LocalBackupPlugin();
        var job = new BackupJob(new ThrowingBackupService());

        Assert.Equal("Darhous.Backup.Local", plugin.Identity.Id);
        Assert.Equal("Darhous", plugin.Identity.Publisher);
        Assert.False(job.IsSafeToResumeAfterCrash);
    }

    [Fact]
    public async Task BackupJob_ExecutesServiceOffTheUiPathAndForwardsProgress()
    {
        var service = new RecordingBackupService();
        var job = new BackupJob(service);
        var requestedBy = Guid.NewGuid();
        var context = new RecordingJobContext(new JobMetadata(
            Guid.NewGuid(), "Darhous.Backup.Local", nameof(BackupJob), JobStatus.Running, 0,
            requestedBy, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, 0, null, null, null,
            JsonSerializer.Serialize(new BackupJobPayload(BackupType.Full, "C:\\backup", requestedBy))));

        await job.ExecuteAsync(context, CancellationToken.None);

        Assert.Equal(BackupType.Full, service.Request!.Type);
        Assert.Equal("C:\\backup", service.Request.DestinationDirectory);
        Assert.Equal(requestedBy, service.Request.RequestedBy);
        Assert.Equal([40], context.Progress);
    }

    private sealed class ThrowingBackupService : ILocalBackupService
    {
        public Task<BackupResult> CreateBackupAsync(
            BackupRequest request,
            Func<int, CancellationToken, Task>? reportProgress,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<RestoreResult> RestoreAsync(string packagePath, Guid? requestedBy, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingBackupService : ILocalBackupService
    {
        public BackupRequest? Request { get; private set; }

        public async Task<BackupResult> CreateBackupAsync(
            BackupRequest request,
            Func<int, CancellationToken, Task>? reportProgress,
            CancellationToken cancellationToken)
        {
            Request = request;
            await reportProgress!(40, cancellationToken);
            return new BackupResult(Guid.NewGuid(), request.Type, "package", 1, new string('0', 64));
        }

        public Task<RestoreResult> RestoreAsync(string packagePath, Guid? requestedBy, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingJobContext(JobMetadata metadata) : IJobContext
    {
        public JobMetadata Metadata { get; } = metadata;
        public List<int> Progress { get; } = [];

        public Task ReportProgressAsync(int percent, CancellationToken cancellationToken)
        {
            Progress.Add(percent);
            return Task.CompletedTask;
        }
    }
}
