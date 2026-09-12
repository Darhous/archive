using Darhous.Archive.Core.Jobs;
using Darhous.Archive.Modules.Discovery.Drives;
using Darhous.Archive.Modules.Discovery.Exclusions;
using Darhous.Archive.Modules.Discovery.Jobs;
using Darhous.Archive.Modules.Discovery.Scanning;
using Darhous.Archive.Modules.Discovery.WatchFolders;
using Darhous.Archive.Modules.Discovery.Watching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Darhous.Archive.Modules.Discovery;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Discovery module's services, its three <see cref="IBackgroundJob"/> types
    /// (consumed by <c>JobRunner</c> — call <c>AddJobRunner()</c> too, order doesn't matter),
    /// and its two always-on background services (file watching, hourly/daily reconciliation).
    /// </summary>
    public static IServiceCollection AddDiscoveryModule(this IServiceCollection services)
    {
        services.AddSingleton<IExclusionService, ExclusionService>();
        services.AddSingleton<IDriveEnumerator, DriveEnumerator>();
        services.AddSingleton<IWatchFolderService, WatchFolderService>();
        services.AddSingleton<IDiscoveryScanner, DiscoveryScanner>();
        services.AddSingleton<IDiscoveryOrchestrator, DiscoveryOrchestrator>();

        services.AddSingleton<IBackgroundJob, DiscoveryScanJob>();
        services.AddSingleton<IBackgroundJob, FileIndexJob>();
        services.AddSingleton<IBackgroundJob, MissingFileReconcileJob>();

        services.AddSingleton<FileSystemWatcherService>();
        services.AddHostedService(sp => sp.GetRequiredService<FileSystemWatcherService>());

        services.AddSingleton<DiscoveryReconciliationService>();
        services.AddHostedService(sp => sp.GetRequiredService<DiscoveryReconciliationService>());

        return services;
    }
}
