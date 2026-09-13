using Darhous.Archive.Core.Jobs;
using Darhous.Archive.Modules.Plugins.Packaging;
using Darhous.Archive.Modules.Updates.Jobs;
using Darhous.Archive.Modules.Updates.Packaging;
using Darhous.Archive.Modules.Updates.Persistence;
using Darhous.Archive.Modules.Updates.Sources;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Writes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Darhous.Archive.Modules.Updates;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Phase 19 updater. Call after Persistence, Local Backup and Plugins, and
    /// before the host starts. JobRunner performs install/rollback work off the UI thread.
    /// </summary>
    public static IServiceCollection AddUpdatesModule(
        this IServiceCollection services,
        UpdateModuleOptions? options = null)
    {
        services.AddSingleton(options ?? new UpdateModuleOptions());
        services.AddSingleton<IUpdateSource, LocalManifestUpdateSource>();
        services.AddSingleton<UpdatePackageValidator>();
        services.AddSingleton(sp => new UpdateHistoryStore(
            sp.GetRequiredKeyedService<ISqliteWriteQueue>(DatabaseKind.Archive),
            sp.GetRequiredService<Core.Time.IClock>()));
        services.AddSingleton<IUpdateSettingsService, UpdateSettingsService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<IUpdateRequestService, UpdateRequestService>();
        services.AddSingleton<IBackgroundJob, UpdateJob>();
        services.AddSingleton<UpdateStartupService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<UpdateStartupService>());
        return services;
    }
}
