using Darhous.Archive.Core.Events;
using Darhous.Archive.Core.Health;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Modules.Plugins.Hosting;
using Darhous.Archive.Modules.Plugins.Lifecycle;
using Darhous.Archive.Modules.Plugins.Packaging;
using Darhous.Archive.PluginSdk;
using Darhous.Archive.Security.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Modules.Plugins;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// <paramref name="coreVersion"/> is the running Core version manifests are checked
    /// against (Plugin SDK §13). <paramref name="exposeHostServices"/> is the one place that
    /// decides exactly which official host services (Plugin SDK §23) a plugin can resolve —
    /// callers wire it once at the composition root, e.g.
    /// <c>registry => registry.AddSingleton(sp.GetRequiredService&lt;IDocumentService&gt;())</c>.
    /// </summary>
    public static IServiceCollection AddPluginsModule(
        this IServiceCollection services, Version coreVersion, Action<ServiceRegistry> exposeHostServices, PluginPlatformOptions? options = null)
    {
        services.AddSingleton(options ?? new PluginPlatformOptions());

        services.AddSingleton<IPluginTrustStore>(sp => new FilePluginTrustStore(sp.GetRequiredService<PluginPlatformOptions>().TrustStoreDirectory));

        services.AddSingleton(sp => new PluginPackageValidator(sp.GetRequiredService<IPluginTrustStore>(), coreVersion));

        services.AddSingleton<PluginInstaller>();

        services.AddSingleton(sp => new InProcessPluginHost(
            sp.GetRequiredService<IEventBus>(),
            sp.GetRequiredService<IHealthRegistry>(),
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<ISecretProtector>(),
            coreVersion));

        services.AddSingleton(sp => new PluginLifecycleManager(
            sp.GetRequiredService<Darhous.Archive.Application.Persistence.IUnitOfWork>(),
            sp.GetRequiredService<PluginInstaller>(),
            sp.GetRequiredService<InProcessPluginHost>(),
            sp.GetRequiredService<PluginPlatformOptions>(),
            exposeHostServices,
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<ILogger<PluginLifecycleManager>>()));

        return services;
    }
}
