using System.Text.Json;
using Darhous.Archive.Core.Jobs;

namespace Darhous.Archive.Modules.Updates.Jobs;

public sealed class UpdateJob(IUpdateService updateService) : IBackgroundJob
{
    public string JobType => nameof(UpdateJob);

    // A crash can leave an external package staged or only some replaceable binaries moved.
    // Never silently replay it; require review of the rollback point/history first.
    public bool IsSafeToResumeAfterCrash => false;

    public async Task ExecuteAsync(IJobContext context, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize(
            context.Metadata.PayloadJson ?? string.Empty,
            UpdateJsonContext.Default.UpdateJobPayload)
            ?? throw new InvalidOperationException("UpdateJob requires a payload.");

        if (string.Equals(payload.Operation, "install", StringComparison.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(payload.PackagePath);
            await updateService.InstallAsync(
                payload.PackagePath,
                payload.InitiatedBy,
                context.ReportProgressAsync,
                cancellationToken);
            return;
        }

        if (string.Equals(payload.Operation, "rollback", StringComparison.Ordinal) && payload.HistoryId.HasValue)
        {
            await updateService.RollbackAsync(
                payload.HistoryId.Value,
                payload.InitiatedBy,
                context.ReportProgressAsync,
                cancellationToken);
            return;
        }

        throw new InvalidOperationException($"Unknown update operation '{payload.Operation}'.");
    }
}
