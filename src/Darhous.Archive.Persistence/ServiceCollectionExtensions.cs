using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Configuration;
using Darhous.Archive.Core.Events;
using Darhous.Archive.Core.Health;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Health;
using Darhous.Archive.Persistence.Jobs;
using Darhous.Archive.Persistence.Outbox;
using Darhous.Archive.Persistence.Repositories;
using Darhous.Archive.Persistence.Transactions;
using Darhous.Archive.Persistence.Writes;

namespace Darhous.Archive.Persistence;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Wires connections, one write queue per database, the archive Unit of Work, the
    /// Phase-2 proof-of-concept repository, per-database health contributors, and the
    /// Outbox-backed <see cref="IEventBus"/> that replaces Phase 1's in-memory-only bus.
    /// Call <see cref="PersistenceInitializer.InitializeAsync"/> before starting the host —
    /// this method only registers services, it does not run migrations.
    /// </summary>
    public static IServiceCollection AddPersistence(
        this IServiceCollection services, PersistenceOptions? options = null)
    {
        services.AddSingleton(options ?? new PersistenceOptions());

        services.AddSingleton<ISqliteConnectionFactory, SqliteConnectionFactory>();

        foreach (var database in Enum.GetValues<DatabaseKind>())
        {
            services.AddKeyedSingleton<SqliteWriteQueue>(database, (sp, key) =>
                new SqliteWriteQueue(
                    (DatabaseKind)key!,
                    sp.GetRequiredService<ISqliteConnectionFactory>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SqliteWriteQueue>>()));

            services.AddKeyedSingleton<ISqliteWriteQueue>(
                database, (sp, key) => sp.GetRequiredKeyedService<SqliteWriteQueue>(key));

            services.AddSingleton<IHostedService>(sp => sp.GetRequiredKeyedService<SqliteWriteQueue>(database));

            services.AddSingleton<IHealthContributor>(sp =>
                new SqliteDatabaseHealthContributor(database, sp.GetRequiredService<ISqliteConnectionFactory>()));
        }

        services.AddSingleton<IUnitOfWork>(sp =>
            new SqliteUnitOfWork(sp.GetRequiredKeyedService<ISqliteWriteQueue>(DatabaseKind.Archive)));

        services.AddSingleton<IAppSettingsStore>(sp => new SqliteAppSettingsStore(
            sp.GetRequiredService<ISqliteConnectionFactory>(),
            sp.GetRequiredKeyedService<ISqliteWriteQueue>(DatabaseKind.Archive),
            sp.GetRequiredService<Core.Time.IClock>()));

        services.AddSingleton<IRoleRepository>(sp =>
            new RoleRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));

        services.AddSingleton<IAppUserRepository>(sp =>
            new AppUserRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));

        services.AddSingleton<ISessionRepository>(sp =>
            new SessionRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));

        services.AddSingleton<IDocumentRepository>(sp =>
            new DocumentRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));

        services.AddSingleton<IDocumentVersionRepository>(sp =>
            new DocumentVersionRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));

        services.AddSingleton<IFolderRepository>(sp =>
            new FolderRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));

        services.AddSingleton<IJobRepository>(sp =>
            new JobRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));

        services.AddSingleton<IWatchFolderRepository>(sp =>
            new WatchFolderRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));

        services.AddSingleton<ISourceExclusionRepository>(sp =>
            new SourceExclusionRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));

        services.AddSingleton<ISourceDriveRepository>(sp =>
            new SourceDriveRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));

        services.AddSingleton<IPluginRegistryRepository>(sp =>
            new PluginRegistryRepository(sp.GetRequiredService<ISqliteConnectionFactory>()));

        services.AddSingleton<InMemoryEventBus>();
        services.AddSingleton<IEventBus, OutboxEventBus>();

        return services;
    }

    /// <summary>
    /// Wires the Job System runner (SAD §51-53). Call after every module that registers its
    /// own <see cref="Core.Jobs.IBackgroundJob"/> implementations via <c>AddSingleton&lt;IBackgroundJob, X&gt;()</c> —
    /// registration order doesn't actually matter (the runner resolves the full
    /// <c>IEnumerable&lt;IBackgroundJob&gt;</c> once at startup), but calling this last keeps
    /// composition roots readable.
    /// </summary>
    public static IServiceCollection AddJobRunner(this IServiceCollection services, JobRunnerOptions? options = null)
    {
        services.AddSingleton(options ?? new JobRunnerOptions());
        services.AddSingleton<JobRunner>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<JobRunner>());
        return services;
    }
}
