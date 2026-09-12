using Darhous.Archive.Core.Health;
using Darhous.Archive.PluginSdk;

namespace Darhous.Archive.Modules.Plugins.Hosting;

/// <summary>Everything the host needs to keep alive for a running plugin, and to tear down cleanly on Stop/Disable/Uninstall.</summary>
public sealed class LoadedPluginHandle(
    PluginAssemblyLoadContext loadContext, IArchivePlugin plugin, ServiceRegistry serviceRegistry,
    EventSubscriptionRegistry eventSubscriptions, BackgroundTaskRegistry backgroundTasks,
    UiExtensionRegistry uiExtensions, IHealthContributor? healthContributor)
    : IAsyncDisposable
{
    public IArchivePlugin Plugin { get; } = plugin;
    public UiExtensionRegistry UiExtensions { get; } = uiExtensions;
    public IHealthContributor? HealthContributor { get; } = healthContributor;

    internal ServiceRegistry ServiceRegistry { get; } = serviceRegistry;
    internal BackgroundTaskRegistry BackgroundTasks { get; } = backgroundTasks;

    public async ValueTask DisposeAsync()
    {
        eventSubscriptions.Dispose();
        await BackgroundTasks.DisposeAsync();
        loadContext.Unload();
    }
}
