using System.Text.Json;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Core.Jobs;
using Darhous.Archive.Modules.Discovery.Exclusions;
using Darhous.Archive.Modules.Discovery.Scanning;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Modules.Discovery.Jobs;

/// <summary>
/// Implementation Plan §53 (Discovery Job Model) — scans exactly one root (one watch folder or
/// one drive) per execution; a logical "Initial Discovery" spans several of these sharing one
/// <c>discovery_runs</c> row (created by whoever kicks off the run — Onboarding or the
/// reconciliation scheduler — not by this job itself). Metadata-only per §54: never reads file
/// content, only queues a <see cref="FileIndexJob"/> per newly-discovered file.
/// </summary>
public sealed class DiscoveryScanJob(
    IUnitOfWork unitOfWork, IExclusionService exclusionService, IDiscoveryScanner scanner,
    IDocumentVersionRepository documentVersionRepository, ILogger<DiscoveryScanJob> logger)
    : IBackgroundJob
{
    public string JobType => nameof(DiscoveryScanJob);

    public async Task ExecuteAsync(IJobContext context, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<DiscoveryScanJobPayload>(context.Metadata.PayloadJson ?? "")
            ?? throw new InvalidOperationException("DiscoveryScanJob requires a payload.");

        await RunAsync(payload, cancellationToken);
    }

    private async Task RunAsync(DiscoveryScanJobPayload payload, CancellationToken cancellationToken)
    {
        var exclusions = await exclusionService.LoadActiveExclusionsAsync(cancellationToken);
        var result = scanner.Scan(payload.RootPath, payload.IncludeSubfolders, exclusions, cancellationToken);

        var queuedCount = 0;
        foreach (var file in result.SupportedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var alreadyKnown = await documentVersionRepository.FindByFilePathAsync(file.FullPath, cancellationToken);
            if (alreadyKnown is not null)
            {
                continue;
            }

            await unitOfWork.ExecuteAsync<object?>(async (uowContext, ct) =>
            {
                var indexPayload = new FileIndexJobPayload(file.FullPath, payload.ImportMode, payload.DestinationFolderId, payload.SourceType, payload.RequestedBy);
                await uowContext.Jobs.CreateAsync(
                    new NewJob(nameof(FileIndexJob), nameof(DiscoveryScanJob), null, payload.RequestedBy,
                        JsonSerializer.Serialize(indexPayload), MaxRetries: 3, Priority: 0, CorrelationId: payload.DiscoveryRunUid),
                    ct);
                return null;
            }, cancellationToken);

            queuedCount++;
        }

        await unitOfWork.ExecuteAsync<object?>(async (uowContext, ct) =>
        {
            await uowContext.DiscoveryRuns.IncrementCountersAsync(
                payload.DiscoveryRunUid, result.FilesSeen, result.SupportedFiles.Count, queuedCount, result.SkippedByExclusion, 0, result.ErrorCount, ct);
            return null;
        }, cancellationToken);

        logger.LogInformation(
            "Discovery scan of {RootPath} complete: {FilesSeen} seen, {Supported} supported, {Queued} queued, {Skipped} excluded, {Errors} errors",
            payload.RootPath, result.FilesSeen, result.SupportedFiles.Count, queuedCount, result.SkippedByExclusion, result.ErrorCount);
    }
}
