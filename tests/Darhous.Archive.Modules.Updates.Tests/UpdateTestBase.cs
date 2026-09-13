using Darhous.Archive.Core.Time;
using Darhous.Archive.Modules.Plugins.Packaging;
using Darhous.Archive.Persistence;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Jobs;
using Darhous.Backup.Local;
using Darhous.Search.SqliteFts.Rebuild;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Darhous.Archive.Modules.Updates.Tests;

public abstract class UpdateTestBase : IAsyncLifetime
{
    private readonly List<IHostedService> _started = [];

    protected string Root { get; private set; } = null!;
    protected string InstallDirectory { get; private set; } = null!;
    protected string PackageDirectory { get; private set; } = null!;
    protected string BackupDirectory { get; private set; } = null!;
    protected string FeedPath { get; private set; } = null!;
    protected PersistenceOptions PersistenceOptions { get; private set; } = null!;
    protected ServiceProvider Provider { get; private set; } = null!;
    protected IPluginTrustStore TrustStore { get; private set; } = null!;
    protected ToggleBackupService BackupGate { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Root = Path.Combine(Path.GetTempPath(), "Darhous.Updates.Tests", Guid.NewGuid().ToString("N"));
        InstallDirectory = Path.Combine(Root, "install");
        PackageDirectory = Path.Combine(Root, "packages");
        BackupDirectory = Path.Combine(Root, "backups");
        FeedPath = Path.Combine(Root, "feed.json");
        foreach (var path in new[] { InstallDirectory, PackageDirectory, BackupDirectory })
        {
            Directory.CreateDirectory(path);
        }

        PersistenceOptions = new PersistenceOptions { DataDirectory = Path.Combine(Root, "data") };
        await PersistenceInitializer.InitializeAsync(PersistenceOptions, CancellationToken.None);
        Provider = await BuildProviderAsync();
    }

    private async Task<ServiceProvider> BuildProviderAsync()
    {
        var options = new UpdateModuleOptions
        {
            InstalledVersion = new Version(1, 0, 0),
            InstallDirectory = InstallDirectory,
            FeedManifestPath = FeedPath,
            StagingDirectory = Path.Combine(Root, "staging"),
            RollbackDirectory = Path.Combine(Root, "rollback"),
            ActiveVersionFilePath = Path.Combine(Root, "active-version.txt"),
            SafetyBackupDirectory = BackupDirectory,
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IClock, SystemClock>();
        services.AddPersistence(PersistenceOptions);
        services.AddSingleton<ISearchIndexRebuilder, NoOpSearchRebuilder>();
        services.AddLocalBackupPlugin(new LocalBackupOptions
        {
            ManagedFilesDirectory = Path.Combine(Root, "managed"),
            SettingsDirectory = Path.Combine(Root, "settings"),
            SafetyBackupDirectory = BackupDirectory,
            WorkingDirectory = Path.Combine(Root, "backup-working"),
            ApplicationVersion = new Version(1, 0, 0),
        });
        var backupGate = new ToggleBackupService();
        services.AddSingleton<ILocalBackupService>(backupGate);

        var trustStore = new FilePluginTrustStore(Path.Combine(Root, "trust"));
        services.AddSingleton<IPluginTrustStore>(trustStore);
        services.AddUpdatesModule(options);
        services.AddJobRunner(new JobRunnerOptions { PollInterval = TimeSpan.FromHours(1) });
        var provider = services.BuildServiceProvider();
        TrustStore = trustStore;
        BackupGate = backupGate;
        backupGate.Inner = provider.GetServices<ILocalBackupService>().First(service => service != backupGate);

        foreach (var hostedService in provider.GetServices<IHostedService>())
        {
            if (hostedService is JobRunner)
            {
                continue;
            }

            await hostedService.StartAsync(CancellationToken.None);
            _started.Add(hostedService);
        }

        return provider;
    }

    public async Task DisposeAsync()
    {
        foreach (var hostedService in _started.AsEnumerable().Reverse())
        {
            await hostedService.StopAsync(CancellationToken.None);
        }

        await Provider.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
        catch (IOException) { }
    }

    protected sealed class NoOpSearchRebuilder : ISearchIndexRebuilder
    {
        public Task RebuildAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    protected sealed class ToggleBackupService : ILocalBackupService
    {
        public ILocalBackupService Inner { get; set; } = null!;
        public bool FailCreate { get; set; }

        public Task<BackupResult> CreateBackupAsync(
            BackupRequest request,
            Func<int, CancellationToken, Task>? reportProgress,
            CancellationToken cancellationToken) =>
            FailCreate
                ? Task.FromException<BackupResult>(new IOException("Injected backup failure."))
                : Inner.CreateBackupAsync(request, reportProgress, cancellationToken);

        public Task<RestoreResult> RestoreAsync(
            string packagePath,
            Guid? requestedBy,
            CancellationToken cancellationToken) => Inner.RestoreAsync(packagePath, requestedBy, cancellationToken);
    }
}
