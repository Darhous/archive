using Darhous.Archive.Audit.Writing;
using Darhous.Archive.Core.Events;
using Darhous.Archive.Core.Health;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Modules.Plugins.Hosting;
using Darhous.Archive.Modules.Plugins.Lifecycle;
using Darhous.Archive.Modules.Plugins.Packaging;
using Darhous.Archive.Persistence;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Transactions;
using Darhous.Archive.Persistence.Writes;
using Darhous.Archive.Security.Secrets;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darhous.Archive.Modules.Plugins.Tests;

/// <summary>Same isolated-temp-directory pattern as the other module test bases — Plugins/PluginData/PluginTrust all point under one throwaway root, never the real %ProgramData%.</summary>
public abstract class PluginsTestBase : IAsyncLifetime
{
    private readonly string _dbDirectory = Path.Combine(Path.GetTempPath(), "darhous-plugins-tests-db", Guid.NewGuid().ToString("N"));
    protected readonly string PluginsRoot = Path.Combine(Path.GetTempPath(), "darhous-plugins-tests-plugins", Guid.NewGuid().ToString("N"));
    private readonly string _pluginDataRoot = Path.Combine(Path.GetTempPath(), "darhous-plugins-tests-plugindata", Guid.NewGuid().ToString("N"));
    private readonly string _trustDirectory = Path.Combine(Path.GetTempPath(), "darhous-plugins-tests-trust", Guid.NewGuid().ToString("N"));
    protected readonly string PackageDirectory = Path.Combine(Path.GetTempPath(), "darhous-plugins-tests-packages", Guid.NewGuid().ToString("N"));

    private SqliteWriteQueue? _archiveWriteQueue;
    private SqliteWriteQueue? _auditWriteQueue;
    private BufferedAuditService? _auditService;

    protected SqliteUnitOfWork UnitOfWork { get; private set; } = null!;
    protected IPluginTrustStore TrustStore { get; private set; } = null!;
    protected PluginLifecycleManager LifecycleManager { get; private set; } = null!;
    protected HealthRegistry HealthRegistry { get; private set; } = null!;
    protected List<ServiceRegistry> ExposedHostServiceCalls { get; } = [];

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_dbDirectory);
        Directory.CreateDirectory(PluginsRoot);
        Directory.CreateDirectory(_pluginDataRoot);
        Directory.CreateDirectory(_trustDirectory);
        Directory.CreateDirectory(PackageDirectory);

        var persistenceOptions = new PersistenceOptions { DataDirectory = _dbDirectory };
        await PersistenceInitializer.InitializeAsync(persistenceOptions, CancellationToken.None);

        var connectionFactory = new SqliteConnectionFactory(persistenceOptions);

        _archiveWriteQueue = new SqliteWriteQueue(DatabaseKind.Archive, connectionFactory, NullLogger<SqliteWriteQueue>.Instance);
        await _archiveWriteQueue.StartAsync(CancellationToken.None);

        _auditWriteQueue = new SqliteWriteQueue(DatabaseKind.Audit, connectionFactory, NullLogger<SqliteWriteQueue>.Instance);
        await _auditWriteQueue.StartAsync(CancellationToken.None);

        _auditService = new BufferedAuditService(_auditWriteQueue, connectionFactory, new SystemClock(), NullLogger<BufferedAuditService>.Instance);
        await _auditService.StartAsync(CancellationToken.None);

        UnitOfWork = new SqliteUnitOfWork(_archiveWriteQueue);

        var options = new PluginPlatformOptions
        {
            PluginsRoot = PluginsRoot,
            PluginDataRoot = _pluginDataRoot,
            TrustStoreDirectory = _trustDirectory,
        };

        TrustStore = new FilePluginTrustStore(options.TrustStoreDirectory);
        var validator = new PluginPackageValidator(TrustStore, new Version(1, 0, 0));
        var installer = new PluginInstaller(UnitOfWork, validator, options);

        HealthRegistry = new HealthRegistry([], NullLogger<HealthRegistry>.Instance);
        var host = new InProcessPluginHost(
            new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance),
            HealthRegistry,
            NullLoggerFactory.Instance,
            new FakeSecretProtector(),
            new Version(1, 0, 0));

        LifecycleManager = new PluginLifecycleManager(
            UnitOfWork, installer, host, options,
            registry => ExposedHostServiceCalls.Add(registry),
            new SystemClock(),
            NullLogger<PluginLifecycleManager>.Instance);
    }

    public async Task DisposeAsync()
    {
        if (_auditService is not null) await _auditService.StopAsync(CancellationToken.None);
        if (_auditWriteQueue is not null) await _auditWriteQueue.StopAsync(CancellationToken.None);
        if (_archiveWriteQueue is not null) await _archiveWriteQueue.StopAsync(CancellationToken.None);

        foreach (var dir in new[] { _dbDirectory, PluginsRoot, _pluginDataRoot, _trustDirectory, PackageDirectory })
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }

    private sealed class FakeSecretProtector : ISecretProtector
    {
        public string Protect(string plaintext) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext));
        public string Unprotect(string protectedValue) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedValue));
    }
}
