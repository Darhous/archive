using Darhous.Archive.Audit.Writing;
using Darhous.Archive.Contracts.Documents;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Modules.Documents.BulkOperations;
using Darhous.Archive.Modules.Documents.Services;
using Darhous.Archive.Modules.Documents.Storage;
using Darhous.Archive.Modules.Folders;
using Darhous.Archive.Persistence;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Repositories;
using Darhous.Archive.Persistence.Transactions;
using Darhous.Archive.Persistence.Writes;
using Darhous.Search.SqliteFts;
using Darhous.Search.SqliteFts.Availability;
using Darhous.Search.SqliteFts.Indexing;
using Darhous.Search.SqliteFts.Query;
using Darhous.Search.SqliteFts.Rebuild;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darhous.Search.SqliteFts.Tests;

/// <summary>Same isolated-temp-DB pattern as the other module test bases (e.g. FoldersTestBase) — wires the real archive.db + search.db write queues so the reconciliation/index/query pipeline runs against real SQLite, not mocks.</summary>
public abstract class SearchTestBase : IAsyncLifetime
{
    private readonly string _dbDirectory = Path.Combine(Path.GetTempPath(), "darhous-search-tests-db", Guid.NewGuid().ToString("N"));
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "darhous-search-tests-storage", Guid.NewGuid().ToString("N"));
    private readonly string _sourceFilesDirectory = Path.Combine(Path.GetTempPath(), "darhous-search-tests-sources", Guid.NewGuid().ToString("N"));

    private SqliteWriteQueue? _archiveWriteQueue;
    private SqliteWriteQueue? _auditWriteQueue;
    private SqliteWriteQueue? _searchWriteQueue;
    private BufferedAuditService? _auditService;

    protected SqliteConnectionFactory ConnectionFactory { get; private set; } = null!;
    protected SqliteUnitOfWork UnitOfWork { get; private set; } = null!;
    protected DocumentService DocumentService { get; private set; } = null!;
    protected FolderService FolderService { get; private set; } = null!;
    protected BulkOperationService BulkOperationService { get; private set; } = null!;

    protected ISearchAvailability Availability { get; private set; } = null!;
    protected ISearchIndexWriter IndexWriter { get; private set; } = null!;
    protected SearchReconciliationService ReconciliationService { get; private set; } = null!;
    protected ISearchIndexRebuilder Rebuilder { get; private set; } = null!;
    protected IFtsQueryService QueryService { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_dbDirectory);
        Directory.CreateDirectory(_storageRoot);
        Directory.CreateDirectory(_sourceFilesDirectory);

        var persistenceOptions = new PersistenceOptions { DataDirectory = _dbDirectory };
        await PersistenceInitializer.InitializeAsync(persistenceOptions, CancellationToken.None);

        ConnectionFactory = new SqliteConnectionFactory(persistenceOptions);

        _archiveWriteQueue = new SqliteWriteQueue(DatabaseKind.Archive, ConnectionFactory, NullLogger<SqliteWriteQueue>.Instance);
        await _archiveWriteQueue.StartAsync(CancellationToken.None);

        _auditWriteQueue = new SqliteWriteQueue(DatabaseKind.Audit, ConnectionFactory, NullLogger<SqliteWriteQueue>.Instance);
        await _auditWriteQueue.StartAsync(CancellationToken.None);

        _searchWriteQueue = new SqliteWriteQueue(DatabaseKind.Search, ConnectionFactory, NullLogger<SqliteWriteQueue>.Instance);
        await _searchWriteQueue.StartAsync(CancellationToken.None);

        _auditService = new BufferedAuditService(_auditWriteQueue, ConnectionFactory, new SystemClock(), NullLogger<BufferedAuditService>.Instance);
        await _auditService.StartAsync(CancellationToken.None);

        UnitOfWork = new SqliteUnitOfWork(_archiveWriteQueue);

        var storageOptions = new DocumentStorageOptions { ArchiveStorageRoot = _storageRoot };
        DocumentService = new DocumentService(UnitOfWork, new FileStorageService(storageOptions), new SystemClock(), _auditService, NullLogger<DocumentService>.Instance);
        FolderService = new FolderService(UnitOfWork, _auditService);
        BulkOperationService = new BulkOperationService(UnitOfWork, new SystemClock(), _auditService);

        Availability = new SearchAvailability(NullLogger<SearchAvailability>.Instance);
        IndexWriter = new SearchIndexWriter(_searchWriteQueue, Availability, NullLogger<SearchIndexWriter>.Instance);

        var documentRepository = new DocumentRepository(ConnectionFactory);
        var documentVersionRepository = new DocumentVersionRepository(ConnectionFactory);

        ReconciliationService = new SearchReconciliationService(
            documentRepository, documentVersionRepository, IndexWriter, ConnectionFactory,
            new SearchIndexingOptions(), NullLogger<SearchReconciliationService>.Instance);

        Rebuilder = new SearchIndexRebuilder(_searchWriteQueue, ReconciliationService, Availability, NullLogger<SearchIndexRebuilder>.Instance);
        QueryService = new FtsQueryService(ConnectionFactory, Availability, _auditService, NullLogger<FtsQueryService>.Instance);
    }

    public async Task DisposeAsync()
    {
        if (_auditService is not null) await _auditService.StopAsync(CancellationToken.None);
        if (_auditWriteQueue is not null) await _auditWriteQueue.StopAsync(CancellationToken.None);
        if (_archiveWriteQueue is not null) await _archiveWriteQueue.StopAsync(CancellationToken.None);
        if (_searchWriteQueue is not null) await _searchWriteQueue.StopAsync(CancellationToken.None);

        foreach (var dir in new[] { _dbDirectory, _storageRoot, _sourceFilesDirectory })
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

    protected string CreateSourceFile(string content = "x")
    {
        var path = Path.Combine(_sourceFilesDirectory, $"{Guid.NewGuid():N}.pdf");
        File.WriteAllText(path, content);
        return path;
    }

    protected async Task<Guid> AddDocumentAsync(string title, Guid? folderId = null)
    {
        var result = await DocumentService.AddDocumentAsync(
            CreateSourceFile(title), title, folderId, DocumentSourceType.Manual,
            DocumentStorageMode.Managed, null, false, CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
