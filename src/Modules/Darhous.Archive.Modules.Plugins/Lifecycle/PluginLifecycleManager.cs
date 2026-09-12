using System.Collections.Concurrent;
using System.Text.Json;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Core.Results;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Modules.Plugins.Hosting;
using Darhous.Archive.PluginSdk;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Modules.Plugins.Lifecycle;

/// <summary>
/// The top-level orchestrator for Plugin SDK §15 (Lifecycle) and §69 (Crash Loop Protection:
/// 3 crashes within 10 minutes → Quarantined, never auto-restarted after that). Holds every
/// currently-running plugin's <see cref="LoadedPluginHandle"/> so Disable/Remove/Update can
/// stop it cleanly.
/// </summary>
public sealed class PluginLifecycleManager(
    IUnitOfWork unitOfWork, PluginInstaller installer, InProcessPluginHost host, PluginPlatformOptions options,
    Action<ServiceRegistry> exposeHostServices, IClock clock, ILogger<PluginLifecycleManager> logger)
{
    private static readonly TimeSpan CrashLoopWindow = TimeSpan.FromMinutes(10);
    private const int CrashLoopThreshold = 3;

    private readonly ConcurrentDictionary<string, LoadedPluginHandle> _running = new();
    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _recentCrashes = new();

    public Task<Result<PluginManifest>> InstallAsync(string packagePath, CancellationToken cancellationToken) =>
        installer.InstallAsync(packagePath, cancellationToken);

    public async Task<Result> EnableAsync(string pluginId, CancellationToken cancellationToken)
    {
        var record = await unitOfWork.ExecuteAsync((context, ct) => context.Plugins.GetByPluginIdAsync(pluginId, ct), cancellationToken);
        if (record is null)
        {
            return Result.Failure(Error.Of("PLUGIN_NOT_FOUND", $"Plugin '{pluginId}' is not installed."));
        }

        if (record.Status == "quarantined")
        {
            return Result.Failure(Error.Of("PLUGIN_QUARANTINED", "This plugin is quarantined after repeated crashes and must be re-enabled explicitly after review (Plugin SDK §70)."));
        }

        var permissions = await unitOfWork.ExecuteAsync((context, ct) => context.Plugins.ListPermissionsAsync(pluginId, ct), cancellationToken);
        var ungranted = permissions.Where(p => !p.Granted).ToList();
        if (ungranted.Count > 0)
        {
            return Result.Failure(Error.Of("PLUGIN_PERMISSIONS_NOT_GRANTED",
                $"The following permissions must be approved before enabling: {string.Join(", ", ungranted.Select(p => p.Permission))}"));
        }

        var pluginDirectory = installer.GetVersionedDirectory(pluginId, record.ActiveVersion);
        var manifest = ReadManifest(pluginDirectory);

        var dependencySnapshots = (await unitOfWork.ExecuteAsync((context, ct) => context.Plugins.ListAllAsync(ct), cancellationToken))
            .Select(p => new PluginDependencyResolver.DependencySnapshot(p.PluginId, p.ActiveVersion, p.Status is "healthy" or "degraded" or "starting", p.Status == "failed"))
            .ToList();

        var dependencyErrors = PluginDependencyResolver.ValidateForEnable(manifest, dependencySnapshots);
        if (dependencyErrors.Count > 0)
        {
            return Result.Failure(Error.Of("PLUGIN_DEPENDENCY_UNMET", string.Join(" | ", dependencyErrors)));
        }

        return await StartInternalAsync(pluginId, manifest, pluginDirectory, cancellationToken);
    }

    private async Task<Result> StartInternalAsync(string pluginId, PluginManifest manifest, string pluginDirectory, CancellationToken cancellationToken)
    {
        await SetStatusAsync(pluginId, "starting", cancellationToken);

        var storageRoot = Path.Combine(options.PluginDataRoot, pluginId);
        var secretsPath = Path.Combine(storageRoot, "secrets.dat");

        var result = await host.LoadAndStartAsync(pluginDirectory, manifest, exposeHostServices, storageRoot, secretsPath, cancellationToken);

        if (!result.IsSuccess)
        {
            await RecordCrashAsync(pluginId, cancellationToken);
            return Result.Failure(result.Error!);
        }

        _running[pluginId] = result.Value;
        await SetStatusAsync(pluginId, "healthy", cancellationToken);
        await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.Plugins.ResetCrashCountAsync(pluginId, ct);
            return null;
        }, cancellationToken);

        return Result.Success();
    }

    public async Task<Result> DisableAsync(string pluginId, CancellationToken cancellationToken)
    {
        if (_running.TryRemove(pluginId, out var handle))
        {
            await SetStatusAsync(pluginId, "stopping", cancellationToken);
            await host.StopAsync(handle, cancellationToken);
        }

        await SetStatusAsync(pluginId, "disabled", cancellationToken);
        return Result.Success();
    }

    public async Task<Result> UpdateAsync(string packagePath, CancellationToken cancellationToken)
    {
        var installResult = await installer.InstallAsync(packagePath, cancellationToken);
        if (!installResult.IsSuccess)
        {
            return Result.Failure(installResult.Error!);
        }

        var manifest = installResult.Value;
        var wasRunning = _running.ContainsKey(manifest.Id);

        if (wasRunning)
        {
            await DisableAsync(manifest.Id, cancellationToken);
        }

        await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.Plugins.SetActiveVersionAsync(manifest.Id, manifest.Version, ct);
            return null;
        }, cancellationToken);
        installer.WriteCurrentVersionMarker(manifest.Id, manifest.Version);

        return wasRunning ? await EnableAsync(manifest.Id, cancellationToken) : Result.Success();
    }

    /// <summary>Plugin SDK §70 — Rollback to a version still present on disk under this plugin's install directory.</summary>
    public async Task<Result> RollbackAsync(string pluginId, string targetVersion, CancellationToken cancellationToken)
    {
        var targetDirectory = installer.GetVersionedDirectory(pluginId, targetVersion);
        if (!Directory.Exists(targetDirectory))
        {
            return Result.Failure(Error.Of("PLUGIN_VERSION_NOT_FOUND", $"Version {targetVersion} of '{pluginId}' is not installed on disk."));
        }

        var wasRunning = _running.ContainsKey(pluginId);
        if (wasRunning)
        {
            await DisableAsync(pluginId, cancellationToken);
        }

        await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.Plugins.SetActiveVersionAsync(pluginId, targetVersion, ct);
            return null;
        }, cancellationToken);
        installer.WriteCurrentVersionMarker(pluginId, targetVersion);

        return wasRunning ? await EnableAsync(pluginId, cancellationToken) : Result.Success();
    }

    public async Task<Result> RemoveAsync(string pluginId, CancellationToken cancellationToken)
    {
        if (_running.ContainsKey(pluginId))
        {
            await DisableAsync(pluginId, cancellationToken);
        }

        await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.Plugins.RemoveAsync(pluginId, ct);
            return null;
        }, cancellationToken);

        var pluginRootDirectory = Path.Combine(options.PluginsRoot, pluginId);
        if (Directory.Exists(pluginRootDirectory))
        {
            Directory.Delete(pluginRootDirectory, recursive: true);
        }

        return Result.Success();
    }

    public async Task GrantPermissionAsync(string pluginId, string permission, Guid grantedBy, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.Plugins.SetPermissionAsync(pluginId, permission, granted: true, grantedBy, clock.UtcNow, ct);
            return null;
        }, cancellationToken);

    private async Task RecordCrashAsync(string pluginId, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var crashes = _recentCrashes.GetOrAdd(pluginId, _ => []);
        lock (crashes)
        {
            crashes.Add(now);
            crashes.RemoveAll(t => now - t > CrashLoopWindow);
        }

        await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.Plugins.IncrementCrashCountAsync(pluginId, ct);
            return null;
        }, cancellationToken);

        if (crashes.Count >= CrashLoopThreshold)
        {
            logger.LogWarning("Plugin {PluginId} crashed {Count} times within {Window} — quarantining (Plugin SDK §69)", pluginId, crashes.Count, CrashLoopWindow);
            await SetStatusAsync(pluginId, "quarantined", cancellationToken);
        }
        else
        {
            await SetStatusAsync(pluginId, "failed", cancellationToken);
        }
    }

    private Task SetStatusAsync(string pluginId, string status, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.Plugins.SetStatusAsync(pluginId, status, ct);
            return null;
        }, cancellationToken);

    private static PluginManifest ReadManifest(string pluginDirectory)
    {
        // The extracted package's own manifest.json — kept alongside the payload in the
        // versioned install directory (PluginInstaller extracts everything except the
        // package's own metadata files, so this is written separately at install time).
        var manifestPath = Path.Combine(pluginDirectory, "manifest.json");
        var json = File.ReadAllText(manifestPath);
        return JsonSerializer.Deserialize<PluginManifest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}
