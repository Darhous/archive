using System.Text.Json;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Modules.Discovery.Drives;
using Darhous.Archive.Modules.Discovery.Jobs;
using Darhous.Archive.Modules.Discovery.WatchFolders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Modules.Discovery;

/// <summary>
/// Implementation Plan §51: hourly reconciliation of watch folders, daily full reconciliation
/// (watch folders + auto-discover drives + missing-file detection). A single global cadence
/// tracked in-memory (not per-folder, not persisted) — simple, and restarting the app just
/// means the next hourly/daily sweep is up to one interval late, which SAD §128's "eventual
/// consistency" tolerance already covers for search; the same tolerance is fine here.
/// </summary>
public sealed class DiscoveryReconciliationService(
    IUnitOfWork unitOfWork, IWatchFolderService watchFolderService, IDriveEnumerator driveEnumerator,
    IClock clock, ILogger<DiscoveryReconciliationService> logger)
    : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan HourlyInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan DailyInterval = TimeSpan.FromDays(1);

    private DateTimeOffset _lastHourly = DateTimeOffset.MinValue;
    private DateTimeOffset _lastDaily = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval);
        do
        {
            try
            {
                var now = clock.UtcNow;

                if (now - _lastDaily >= DailyInterval)
                {
                    await RunReconciliationAsync("daily_full_reconciliation", includeDrives: true, stoppingToken);
                    await QueueMissingFileReconcileAsync(stoppingToken);
                    _lastDaily = now;
                    _lastHourly = now; // daily supersedes the hourly pass for this tick.
                }
                else if (now - _lastHourly >= HourlyInterval)
                {
                    await RunReconciliationAsync("hourly_reconciliation", includeDrives: false, stoppingToken);
                    _lastHourly = now;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Discovery reconciliation tick failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunReconciliationAsync(string runType, bool includeDrives, CancellationToken cancellationToken)
    {
        var runUid = await unitOfWork.ExecuteAsync(
            (context, ct) => context.DiscoveryRuns.StartAsync(runType, requestedBy: null, clock.UtcNow, ct), cancellationToken);

        var rootsQueued = 0;

        foreach (var folder in await watchFolderService.ListEnabledAsync(cancellationToken))
        {
            await QueueScanJobAsync(runUid, folder.Path, folder.IncludeSubfolders, folder.ImportMode, folder.DestinationFolderId, "watch_folder", cancellationToken);
            rootsQueued++;
        }

        if (includeDrives)
        {
            foreach (var drive in (await driveEnumerator.RefreshAndListAsync(cancellationToken)).Where(d => d.IsEnabled && d.AutoDiscover))
            {
                await QueueScanJobAsync(runUid, drive.DriveRoot, includeSubfolders: true, "index_in_place", null, "import", cancellationToken);
                rootsQueued++;
            }
        }

        await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.DiscoveryRuns.CompleteAsync(runUid, "completed", rootsQueued, clock.UtcNow, ct);
            return null;
        }, cancellationToken);

        logger.LogInformation("{RunType} queued {RootsQueued} root scan(s)", runType, rootsQueued);
    }

    private Task QueueScanJobAsync(
        Guid runUid, string rootPath, bool includeSubfolders, string importMode, Guid? destinationFolderId,
        string sourceType, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            var payload = new DiscoveryScanJobPayload(runUid, rootPath, includeSubfolders, importMode, destinationFolderId, sourceType, RequestedBy: null);
            await context.Jobs.CreateAsync(
                new NewJob(nameof(DiscoveryScanJob), nameof(DiscoveryReconciliationService), null, null,
                    JsonSerializer.Serialize(payload), MaxRetries: 1, Priority: 1, CorrelationId: runUid),
                ct);
            return null;
        }, cancellationToken);

    private Task QueueMissingFileReconcileAsync(CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.Jobs.CreateAsync(
                new NewJob(nameof(MissingFileReconcileJob), nameof(DiscoveryReconciliationService), null, null, null, MaxRetries: 1, Priority: 1, CorrelationId: null),
                ct);
            return null;
        }, cancellationToken);
}
