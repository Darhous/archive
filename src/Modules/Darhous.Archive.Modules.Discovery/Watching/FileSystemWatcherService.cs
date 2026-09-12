using System.Text.Json;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Modules.Discovery.Jobs;
using Darhous.Archive.Modules.Discovery.WatchFolders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Modules.Discovery.Watching;

/// <summary>
/// Implementation Plan §49/§51 (File Watcher, near-real-time continuous indexing). Re-syncs
/// its set of <see cref="FileSystemWatcher"/> instances against <c>watch_folders</c> every 5
/// minutes rather than reacting to Onboarding changes immediately — simple and robust; a brand
/// new watch folder starts being watched within 5 minutes of being added, which is fine since
/// the hourly/daily reconciliation sweep would catch anything missed anyway.
/// </summary>
public sealed class FileSystemWatcherService(IWatchFolderService watchFolderService, IUnitOfWork unitOfWork, ILogger<FileSystemWatcherService> logger)
    : BackgroundService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FileReadyTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan FileReadyPollInterval = TimeSpan.FromMilliseconds(250);

    private readonly Dictionary<Guid, FileSystemWatcher> _watchersByFolderUid = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);
        do
        {
            try
            {
                await RefreshWatchersAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to refresh file watch folders");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var watcher in _watchersByFolderUid.Values)
        {
            watcher.Dispose();
        }

        _watchersByFolderUid.Clear();
        return base.StopAsync(cancellationToken);
    }

    private async Task RefreshWatchersAsync(CancellationToken cancellationToken)
    {
        var enabled = await watchFolderService.ListEnabledAsync(cancellationToken);
        var enabledUids = enabled.Select(f => f.Uid).ToHashSet();

        foreach (var (uid, watcher) in _watchersByFolderUid.ToList())
        {
            if (!enabledUids.Contains(uid))
            {
                watcher.Dispose();
                _watchersByFolderUid.Remove(uid);
            }
        }

        foreach (var folder in enabled)
        {
            if (_watchersByFolderUid.ContainsKey(folder.Uid) || !Directory.Exists(folder.Path))
            {
                continue;
            }

            _watchersByFolderUid[folder.Uid] = CreateWatcher(folder);
        }
    }

    private FileSystemWatcher CreateWatcher(WatchFolder folder)
    {
        var watcher = new FileSystemWatcher(folder.Path)
        {
            IncludeSubdirectories = folder.IncludeSubfolders,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
        };

        watcher.Created += (_, e) => _ = HandleNewFileAsync(e.FullPath, folder);
        watcher.Renamed += (_, e) => _ = HandleNewFileAsync(e.FullPath, folder);
        watcher.Error += (_, e) =>
        {
            // Typically an internal buffer overflow under a burst of changes — drop and let the
            // next refresh cycle recreate the watcher rather than trying to resurrect this one.
            logger.LogWarning(e.GetException(), "FileSystemWatcher error for {Path} — will recreate on next refresh", folder.Path);
            watcher.EnableRaisingEvents = false;
            _watchersByFolderUid.Remove(folder.Uid);
            watcher.Dispose();
        };

        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    private async Task HandleNewFileAsync(string fullPath, WatchFolder folder)
    {
        try
        {
            var extension = Path.GetExtension(fullPath);
            if (!DiscoveryDefaults.SupportedExtensions.Contains(extension))
            {
                return;
            }

            var ready = await FileReadinessChecker.WaitUntilReadyAsync(fullPath, FileReadyTimeout, FileReadyPollInterval, CancellationToken.None);
            if (!ready)
            {
                logger.LogWarning("Gave up waiting for {Path} to finish being written", fullPath);
                return;
            }

            var existing = await unitOfWork.ExecuteAsync(
                (context, ct) => context.DocumentVersions.FindByFilePathAsync(fullPath, ct), CancellationToken.None);
            if (existing is not null)
            {
                return;
            }

            var payload = new FileIndexJobPayload(fullPath, folder.ImportMode, folder.DestinationFolderId, "watch_folder", folder.CreatedBy);

            await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
            {
                await context.Jobs.CreateAsync(
                    new NewJob(nameof(FileIndexJob), nameof(FileSystemWatcherService), null, folder.CreatedBy,
                        JsonSerializer.Serialize(payload), MaxRetries: 3, Priority: 10, CorrelationId: null),
                    ct);
                return null;
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // A single bad event must never take the watcher down (SAD §5.1).
            logger.LogError(ex, "Failed to queue index job for newly watched file {Path}", fullPath);
        }
    }
}
