using Darhous.Archive.Audit.Writing;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Modules.Documents.BulkOperations;
using Darhous.Archive.Modules.Documents.Services;
using Darhous.Archive.Modules.Documents.Storage;
using Darhous.Archive.Modules.Folders;
using Darhous.Archive.Persistence;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Transactions;
using Darhous.Archive.Persistence.Writes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darhous.Archive.Modules.Folders.Tests;

/// <summary>Isolated temp DB + storage root per test — same pattern as the other module test bases.</summary>
public abstract class FoldersTestBase : IAsyncLifetime
{
    private readonly string _dbDirectory = Path.Combine(Path.GetTempPath(), "darhous-folders-tests-db", Guid.NewGuid().ToString("N"));
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "darhous-folders-tests-storage", Guid.NewGuid().ToString("N"));
    private readonly string _sourceFilesDirectory = Path.Combine(Path.GetTempPath(), "darhous-folders-tests-sources", Guid.NewGuid().ToString("N"));

    private SqliteWriteQueue? _archiveWriteQueue;
    private SqliteWriteQueue? _auditWriteQueue;
    private BufferedAuditService? _auditService;

    protected SqliteUnitOfWork UnitOfWork { get; private set; } = null!;
    protected FolderService FolderService { get; private set; } = null!;
    protected DocumentService DocumentService { get; private set; } = null!;
    protected BulkOperationService BulkOperationService { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_dbDirectory);
        Directory.CreateDirectory(_storageRoot);
        Directory.CreateDirectory(_sourceFilesDirectory);

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
        FolderService = new FolderService(UnitOfWork, _auditService);

        var storageOptions = new DocumentStorageOptions { ArchiveStorageRoot = _storageRoot };
        DocumentService = new DocumentService(UnitOfWork, new FileStorageService(storageOptions), new SystemClock(), _auditService, NullLogger<DocumentService>.Instance);
        BulkOperationService = new BulkOperationService(UnitOfWork, new SystemClock(), _auditService);
    }

    public async Task DisposeAsync()
    {
        if (_auditService is not null) await _auditService.StopAsync(CancellationToken.None);
        if (_auditWriteQueue is not null) await _auditWriteQueue.StopAsync(CancellationToken.None);
        if (_archiveWriteQueue is not null) await _archiveWriteQueue.StopAsync(CancellationToken.None);

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
            CreateSourceFile(title), title, folderId, Contracts.Documents.DocumentSourceType.Manual,
            Contracts.Documents.DocumentStorageMode.Managed, null, false, CancellationToken.None);
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
