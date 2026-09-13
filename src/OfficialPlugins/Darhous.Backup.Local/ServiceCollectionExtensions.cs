using Darhous.Archive.Core.Jobs;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Writes;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Persistence.Connections;
using Darhous.Search.SqliteFts.Rebuild;
using Darhous.Backup.Local.Jobs;
using Darhous.Backup.Local.Packaging;
using Microsoft.Extensions.DependencyInjection;

namespace Darhous.Backup.Local;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the official provider. Call after Persistence and Search, and before AddJobRunner.</summary>
    public static IServiceCollection AddLocalBackupPlugin(
        this IServiceCollection services,
        LocalBackupOptions? options = null)
    {
        services.AddSingleton(options ?? new LocalBackupOptions());
        services.AddSingleton<BackupPackageValidator>();
        services.AddSingleton(sp => new BackupHistoryStore(
            sp.GetRequiredKeyedService<ISqliteWriteQueue>(DatabaseKind.Archive),
            sp.GetRequiredService<IClock>()));
        services.AddSingleton<ILocalBackupService>(sp => new LocalBackupService(
            sp.GetRequiredService<LocalBackupOptions>(),
            sp.GetRequiredService<PersistenceOptions>(),
            sp.GetRequiredService<ISqliteConnectionFactory>(),
            sp.GetRequiredService<BackupHistoryStore>(),
            sp.GetRequiredService<BackupPackageValidator>(),
            sp.GetRequiredService<ISearchIndexRebuilder>(),
            sp.GetRequiredKeyedService<ISqliteWriteQueue>(DatabaseKind.Archive),
            sp.GetRequiredKeyedService<ISqliteWriteQueue>(DatabaseKind.Audit),
            sp.GetRequiredKeyedService<ISqliteWriteQueue>(DatabaseKind.Search),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LocalBackupService>>()));
        services.AddSingleton<IBackupRequestService, BackupRequestService>();
        services.AddSingleton<IBackgroundJob, BackupJob>();
        services.AddSingleton<LocalBackupPlugin>();
        return services;
    }
}
