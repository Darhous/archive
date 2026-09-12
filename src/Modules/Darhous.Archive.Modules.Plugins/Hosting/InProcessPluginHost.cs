using Darhous.Archive.Core.Events;
using Darhous.Archive.Core.Health;
using Darhous.Archive.Core.Results;
using Darhous.Archive.PluginSdk;
using Darhous.Archive.PluginSdk.Runtime;
using Darhous.Archive.Security.Secrets;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Modules.Plugins.Hosting;

/// <summary>
/// Plugin SDK §80 "In-Process" isolation — the only isolation mode this phase implements
/// (Out-of-Process needs the Worker Infrastructure built in Phase 13). Loads a plugin's
/// entry assembly into its own collectible <see cref="PluginAssemblyLoadContext"/>, wires the
/// SDK contexts, and drives Configure → Start / Stop per Plugin SDK §18-20.
/// </summary>
public sealed class InProcessPluginHost(
    IEventBus hostEventBus, IHealthRegistry hostHealthRegistry, ILoggerFactory loggerFactory,
    ISecretProtector secretProtector, Version coreVersion)
{
    /// <summary>
    /// Loads the assembly just far enough to confirm it will actually work, then immediately
    /// unloads — Plugin SDK §39's "Health check" step during install, run before anything is
    /// committed. Never calls ConfigureAsync/StartAsync; that only happens on Enable.
    /// </summary>
    public static string? DryLoad(string pluginDirectory, string entryPointRelativePath, string entryTypeName)
    {
        var entryAssemblyPath = Path.Combine(pluginDirectory, entryPointRelativePath);
        if (!File.Exists(entryAssemblyPath))
        {
            return $"Entry point '{entryPointRelativePath}' was not found in the package.";
        }

        var loadContext = new PluginAssemblyLoadContext(entryAssemblyPath);
        try
        {
            var assembly = loadContext.LoadFromFile(entryAssemblyPath);
            var entryType = assembly.GetType(entryTypeName);
            if (entryType is null)
            {
                return $"Entry type '{entryTypeName}' was not found in {entryPointRelativePath}.";
            }

            if (!typeof(IArchivePlugin).IsAssignableFrom(entryType))
            {
                return $"Entry type '{entryTypeName}' does not implement IArchivePlugin.";
            }

            if (entryType.GetConstructor(Type.EmptyTypes) is null)
            {
                return $"Entry type '{entryTypeName}' has no public parameterless constructor.";
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"Failed to load plugin assembly: {ex.Message}";
        }
        finally
        {
            loadContext.Unload();
        }
    }

    /// <summary>
    /// Full load: Configure (register services/events/UI/background tasks) then Start.
    /// <paramref name="exposeHostServices"/> lets the caller pre-populate the plugin's service
    /// registry with whatever official services (Plugin SDK §23) it's allowed to see — the
    /// plugin itself never gets a reference to the host's real DI container.
    /// </summary>
    public async Task<Result<LoadedPluginHandle>> LoadAndStartAsync(
        string pluginDirectory, PluginManifest manifest, Action<ServiceRegistry> exposeHostServices,
        string pluginStorageRoot, string pluginSecretsPath, CancellationToken cancellationToken)
    {
        var entryAssemblyPath = Path.Combine(pluginDirectory, manifest.EntryPoint);
        var loadContext = new PluginAssemblyLoadContext(entryAssemblyPath);

        IArchivePlugin plugin;
        try
        {
            var assembly = loadContext.LoadFromFile(entryAssemblyPath);
            var entryType = assembly.GetType(manifest.EntryType)
                ?? throw new InvalidOperationException($"Entry type '{manifest.EntryType}' not found.");
            plugin = (IArchivePlugin)Activator.CreateInstance(entryType)!;
        }
        catch (Exception ex)
        {
            loadContext.Unload();
            return Result<LoadedPluginHandle>.Failure(Error.Of("PLUGIN_LOAD_FAILED", ex.Message));
        }

        var serviceRegistry = new ServiceRegistry();
        exposeHostServices(serviceRegistry);
        var eventSubscriptions = new EventSubscriptionRegistry(hostEventBus);
        var uiExtensions = new UiExtensionRegistry();
        var backgroundTasks = new BackgroundTaskRegistry(loggerFactory.CreateLogger($"Plugin.{manifest.Id}"));
        var pluginLogger = new PluginLogger(manifest.Id, loggerFactory.CreateLogger($"Plugin.{manifest.Id}"));

        var configurationContext = new PluginConfigurationContext(serviceRegistry, eventSubscriptions, uiExtensions, backgroundTasks, hostHealthRegistry);

        try
        {
            await plugin.ConfigureAsync(configurationContext, cancellationToken);

            var runtimeContext = new PluginRuntimeContext(
                serviceRegistry,
                new PluginStorage(pluginStorageRoot),
                new PluginSecrets(pluginSecretsPath, secretProtector),
                pluginLogger,
                new PluginEnvironment(coreVersion, manifest.Version, isDeveloperMode: false, pluginDirectory));

            await plugin.StartAsync(runtimeContext, cancellationToken);
            backgroundTasks.StartAll();

            var healthContributor = serviceRegistry.GetService<IHealthContributor>();
            if (healthContributor is not null)
            {
                hostHealthRegistry.Register(healthContributor);
            }

            return Result<LoadedPluginHandle>.Success(new LoadedPluginHandle(
                loadContext, plugin, serviceRegistry, eventSubscriptions, backgroundTasks, uiExtensions, healthContributor));
        }
        catch (Exception ex)
        {
            eventSubscriptions.Dispose();
            await backgroundTasks.DisposeAsync();
            loadContext.Unload();
            return Result<LoadedPluginHandle>.Failure(Error.Of("PLUGIN_START_FAILED", ex.Message));
        }
    }

    public async Task StopAsync(LoadedPluginHandle handle, CancellationToken cancellationToken)
    {
        if (handle.HealthContributor is not null)
        {
            hostHealthRegistry.Unregister(handle.HealthContributor);
        }

        await handle.Plugin.StopAsync(cancellationToken);
        await handle.DisposeAsync();
    }
}
