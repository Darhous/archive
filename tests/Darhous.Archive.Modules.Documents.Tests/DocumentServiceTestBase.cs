using Darhous.Archive.Audit.Writing;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Modules.Documents.Services;
using Darhous.Archive.Modules.Documents.Storage;
using Darhous.Archive.Persistence;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Transactions;
using Darhous.Archive.Persistence.Writes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darhous.Archive.Modules.Documents.Tests;

/// <summary>Isolated temp DB + isolated temp ArchiveStorage root per test — never touches real %ProgramData%.</summary>
public abstract class DocumentServiceTestBase : IAsyncLifetime
{
    private readonly string _dbDirectory =
        Path.Combine(Path.GetTempPath(), "darhous-documents-tests-db", Guid.NewGuid().ToString("N"));
    private readonly string _storageRoot =
        Path.Combine(Path.GetTempPath(), "darhous-documents-tests-storage", Guid.NewGuid().ToString("N"));
    private readonly string _sourceFilesDirectory =
        Path.Combine(Path.GetTempPath(), "darhous-documents-tests-sources", Guid.NewGuid().ToString("N"));

    private SqliteWriteQueue? _archiveWriteQueue;
    private SqliteWriteQueue? _auditWriteQueue;
    private BufferedAuditService? _auditService;

    protected DocumentStorageOptions StorageOptions { get; private set; } = null!;
    protected IFileStorageService FileStorage { get; private set; } = null!;
    protected DocumentService DocumentService { get; private set; } = null!;
    protected SqliteUnitOfWork UnitOfWork { get; private set; } = null!;

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
        StorageOptions = new DocumentStorageOptions { ArchiveStorageRoot = _storageRoot };
        FileStorage = new FileStorageService(StorageOptions);
        DocumentService = new DocumentService(UnitOfWork, FileStorage, new SystemClock(), _auditService, NullLogger<DocumentService>.Instance);
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

    /// <summary>Creates a throwaway source file (outside ArchiveStorage) with the given content, returns its path.</summary>
    protected string CreateSourceFile(string content = "test content")
    {
        var path = Path.Combine(_sourceFilesDirectory, $"{Guid.NewGuid():N}.pdf");
        File.WriteAllText(path, content);
        return path;
    }
}
