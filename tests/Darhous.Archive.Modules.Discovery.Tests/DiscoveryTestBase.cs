using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Audit.Writing;
using Darhous.Archive.Core.Events;
using Darhous.Archive.Core.Jobs;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Modules.Discovery;
using Darhous.Archive.Modules.Discovery.Drives;
using Darhous.Archive.Modules.Discovery.Exclusions;
using Darhous.Archive.Modules.Discovery.Jobs;
using Darhous.Archive.Modules.Discovery.Scanning;
using Darhous.Archive.Modules.Discovery.WatchFolders;
using Darhous.Archive.Modules.Documents.Services;
using Darhous.Archive.Modules.Documents.Storage;
using Darhous.Archive.Persistence;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Jobs;
using Darhous.Archive.Persistence.Outbox;
using Darhous.Archive.Persistence.Repositories;
using Darhous.Archive.Persistence.Transactions;
using Darhous.Archive.Persistence.Writes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darhous.Archive.Modules.Discovery.Tests;

/// <summary>Same isolated-temp-DB pattern as the other module test bases — wires real SQLite + a JobRunner with every Discovery job type registered.</summary>
public abstract class DiscoveryTestBase : IAsyncLifetime
{
    private readonly string _dbDirectory = Path.Combine(Path.GetTempPath(), "darhous-discovery-tests-db", Guid.NewGuid().ToString("N"));
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "darhous-discovery-tests-storage", Guid.NewGuid().ToString("N"));
    protected readonly string ScanRoot = Path.Combine(Path.GetTempPath(), "darhous-discovery-tests-scan", Guid.NewGuid().ToString("N"));

    private SqliteWriteQueue? _archiveWriteQueue;
    private SqliteWriteQueue? _auditWriteQueue;
    private BufferedAuditService? _auditService;

    protected SqliteUnitOfWork UnitOfWork { get; private set; } = null!;
    protected DocumentService DocumentService { get; private set; } = null!;
    protected IExclusionService ExclusionService { get; private set; } = null!;
    protected IWatchFolderService WatchFolderService { get; private set; } = null!;
    protected IDiscoveryScanner Scanner { get; private set; } = null!;
    protected IDiscoveryOrchestrator Orchestrator { get; private set; } = null!;
    protected JobRunner Runner { get; private set; } = null!;
    protected IDocumentRepository DocumentRepository { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_dbDirectory);
        Directory.CreateDirectory(_storageRoot);
        Directory.CreateDirectory(ScanRoot);

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

        var storageOptions = new DocumentStorageOptions { ArchiveStorageRoot = _storageRoot };
        DocumentService = new DocumentService(UnitOfWork, new FileStorageService(storageOptions), new SystemClock(), _auditService, NullLogger<DocumentService>.Instance);
        DocumentRepository = new DocumentRepository(connectionFactory);
        var documentVersionRepository = new DocumentVersionRepository(connectionFactory);

        ExclusionService = new ExclusionService(UnitOfWork);
        WatchFolderService = new WatchFolderService(UnitOfWork);
        Scanner = new DiscoveryScanner();
        var driveEnumerator = new DriveEnumerator(UnitOfWork, new SystemClock());
        Orchestrator = new DiscoveryOrchestrator(UnitOfWork, WatchFolderService, driveEnumerator, new SystemClock());

        IBackgroundJob[] jobs =
        [
            new DiscoveryScanJob(UnitOfWork, ExclusionService, Scanner, documentVersionRepository, NullLogger<DiscoveryScanJob>.Instance),
            new FileIndexJob(DocumentService, documentVersionRepository, NullLogger<FileIndexJob>.Instance),
            new MissingFileReconcileJob(DocumentRepository, documentVersionRepository, UnitOfWork, NullLogger<MissingFileReconcileJob>.Instance),
        ];
        var eventBus = new OutboxEventBus(new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance), UnitOfWork);
        Runner = new JobRunner(UnitOfWork, jobs, new SystemClock(), new JobRunnerOptions { BatchSize = 10 }, NullLogger<JobRunner>.Instance, eventBus);
    }

    public async Task DisposeAsync()
    {
        if (_auditService is not null) await _auditService.StopAsync(CancellationToken.None);
        if (_auditWriteQueue is not null) await _auditWriteQueue.StopAsync(CancellationToken.None);
        if (_archiveWriteQueue is not null) await _archiveWriteQueue.StopAsync(CancellationToken.None);

        foreach (var dir in new[] { _dbDirectory, _storageRoot, ScanRoot })
        {
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup.
            }
        }
    }

    /// <summary>Drains ready jobs until none remain or the safety cap is hit (a DiscoveryScanJob queues FileIndexJobs, which then need their own pass to run).</summary>
    protected async Task DrainJobsAsync(int maxPasses = 10)
    {
        for (var i = 0; i < maxPasses; i++)
        {
            await Runner.RunOnceAsync(CancellationToken.None);
        }
    }

    protected string CreateFile(string relativePath, string content = "x")
    {
        var fullPath = Path.Combine(ScanRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }
}
