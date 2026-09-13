using System.Text.Json;
using Darhous.Archive.Core.Jobs;

namespace Darhous.Backup.Local.Jobs;

public sealed class BackupJob(ILocalBackupService backupService) : IBackgroundJob
{
    public string JobType => nameof(BackupJob);

    // A normal execution failure may be retried by JobRunner. A process crash is different:
    // the destination may contain an undeletable .partial file (for example after a USB pull),
    // so silently re-running an orphaned execution is not claimed to be safe.
    public bool IsSafeToResumeAfterCrash => false;

    public async Task ExecuteAsync(IJobContext context, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<BackupJobPayload>(context.Metadata.PayloadJson ?? string.Empty)
            ?? throw new InvalidOperationException("BackupJob requires a payload.");
        await backupService.CreateBackupAsync(
            new BackupRequest(payload.Type, payload.DestinationDirectory, payload.RequestedBy),
            context.ReportProgressAsync,
            cancellationToken);
    }
}
