using System.Text.Json;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Core.Results;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Modules.Discovery.Drives;
using Darhous.Archive.Modules.Discovery.Jobs;
using Darhous.Archive.Modules.Discovery.WatchFolders;

namespace Darhous.Archive.Modules.Discovery;

/// <summary>
/// A <c>discovery_runs</c> row here means "every root's scan was successfully queued", not
/// "every file has been indexed yet" — each queued <see cref="DiscoveryScanJob"/> updates the
/// same run's counters as it completes (via its shared <c>DiscoveryRunUid</c> correlation),
/// and each of those in turn queues <see cref="FileIndexJob"/>s that drain independently
/// through the normal job queue. Tracking "fully drained" would need a fan-out/join the V1
/// Job System doesn't have yet — deliberately simplified rather than building that now.
/// </summary>
public sealed class DiscoveryOrchestrator(
    IUnitOfWork unitOfWork, IWatchFolderService watchFolderService, IDriveEnumerator driveEnumerator, IClock clock)
    : IDiscoveryOrchestrator
{
    public async Task<Result<Guid>> StartInitialDiscoveryAsync(Guid? requestedBy, CancellationToken cancellationToken)
    {
        var watchFolders = await watchFolderService.ListEnabledAsync(cancellationToken);
        if (watchFolders.Count == 0)
        {
            return Result<Guid>.Failure(Error.Of("DISCOVERY_NO_WATCH_FOLDERS", "لم يتم اختيار أي مجلد للأرشفة بعد."));
        }

        var runUid = await unitOfWork.ExecuteAsync(
            (context, ct) => context.DiscoveryRuns.StartAsync("initial_discovery", requestedBy, clock.UtcNow, ct), cancellationToken);

        foreach (var folder in watchFolders)
        {
            await QueueScanJobAsync(runUid, folder.Path, folder.IncludeSubfolders, folder.ImportMode, folder.DestinationFolderId, "watch_folder", requestedBy, cancellationToken);
        }

        await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.DiscoveryRuns.CompleteAsync(runUid, "completed", watchFolders.Count, clock.UtcNow, ct);
            return null;
        }, cancellationToken);

        return Result<Guid>.Success(runUid);
    }

    public async Task<Result<Guid>> StartFullComputerScanAsync(Guid? requestedBy, CancellationToken cancellationToken)
    {
        var drives = await driveEnumerator.RefreshAndListAsync(cancellationToken);
        var autoDiscoverDrives = drives.Where(d => d.IsEnabled && d.AutoDiscover).ToList();

        var runUid = await unitOfWork.ExecuteAsync(
            (context, ct) => context.DiscoveryRuns.StartAsync("initial_discovery", requestedBy, clock.UtcNow, ct), cancellationToken);

        foreach (var drive in autoDiscoverDrives)
        {
            // §46.5 default for auto-discovered files: stay where they are, just indexed.
            await QueueScanJobAsync(runUid, drive.DriveRoot, includeSubfolders: true, "index_in_place", destinationFolderId: null, "import", requestedBy, cancellationToken);
        }

        await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.DiscoveryRuns.CompleteAsync(runUid, "completed", autoDiscoverDrives.Count, clock.UtcNow, ct);
            return null;
        }, cancellationToken);

        return Result<Guid>.Success(runUid);
    }

    private Task QueueScanJobAsync(
        Guid runUid, string rootPath, bool includeSubfolders, string importMode, Guid? destinationFolderId,
        string sourceType, Guid? requestedBy, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            var payload = new DiscoveryScanJobPayload(runUid, rootPath, includeSubfolders, importMode, destinationFolderId, sourceType, requestedBy);
            await context.Jobs.CreateAsync(
                new NewJob(nameof(DiscoveryScanJob), nameof(DiscoveryOrchestrator), null, requestedBy,
                    JsonSerializer.Serialize(payload), MaxRetries: 1, Priority: 5, CorrelationId: runUid),
                ct);
            return null;
        }, cancellationToken);
}
