using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Audit.Writing;
using Darhous.Archive.Contracts.Documents;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Modules.Documents.Services;
using Darhous.Archive.Modules.Documents.Storage;
using Darhous.Archive.Modules.Importers.Extractors;
using Darhous.Archive.Persistence;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Repositories;
using Darhous.Archive.Persistence.Transactions;
using Darhous.Archive.Persistence.Writes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darhous.Archive.Modules.Importers.Tests;

public class TextExtractionSweepServiceTests : IAsyncLifetime
{
    private readonly string _dbDirectory = Path.Combine(Path.GetTempPath(), "darhous-importer-db-tests", Guid.NewGuid().ToString("N"));
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "darhous-importer-storage-tests", Guid.NewGuid().ToString("N"));
    private readonly string _sourceRoot = Path.Combine(Path.GetTempPath(), "darhous-importer-source-tests", Guid.NewGuid().ToString("N"));

    private SqliteWriteQueue? _archiveWriteQueue;
    private SqliteWriteQueue? _auditWriteQueue;
    private BufferedAuditService? _auditService;

    private SqliteUnitOfWork _unitOfWork = null!;
    private DocumentService _documentService = null!;
    private DocumentRepository _documentRepository = null!;
    private DocumentVersionRepository _documentVersionRepository = null!;
    private TextExtractionSweepService _sweepService = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_dbDirectory);
        Directory.CreateDirectory(_storageRoot);
        Directory.CreateDirectory(_sourceRoot);

        var persistenceOptions = new PersistenceOptions { DataDirectory = _dbDirectory };
        await PersistenceInitializer.InitializeAsync(persistenceOptions, CancellationToken.None);

        var connectionFactory = new SqliteConnectionFactory(persistenceOptions);

        _archiveWriteQueue = new SqliteWriteQueue(DatabaseKind.Archive, connectionFactory, NullLogger<SqliteWriteQueue>.Instance);
        await _archiveWriteQueue.StartAsync(CancellationToken.None);

        _auditWriteQueue = new SqliteWriteQueue(DatabaseKind.Audit, connectionFactory, NullLogger<SqliteWriteQueue>.Instance);
        await _auditWriteQueue.StartAsync(CancellationToken.None);

        _auditService = new BufferedAuditService(_auditWriteQueue, connectionFactory, new SystemClock(), NullLogger<BufferedAuditService>.Instance);
        await _auditService.StartAsync(CancellationToken.None);

        _unitOfWork = new SqliteUnitOfWork(_archiveWriteQueue);
        _documentRepository = new DocumentRepository(connectionFactory);
        _documentVersionRepository = new DocumentVersionRepository(connectionFactory);

        var storageOptions = new DocumentStorageOptions { ArchiveStorageRoot = _storageRoot };
        _documentService = new DocumentService(_unitOfWork, new FileStorageService(storageOptions), new SystemClock(), _auditService, NullLogger<DocumentService>.Instance);

        IContentExtractor[] extractors = [new PdfContentExtractor(), new OpenXmlContentExtractor(), new EmlContentExtractor()];
        _sweepService = new TextExtractionSweepService(_unitOfWork, extractors, NullLogger<TextExtractionSweepService>.Instance);
    }

    public async Task DisposeAsync()
    {
        if (_auditService is not null) await _auditService.StopAsync(CancellationToken.None);
        if (_auditWriteQueue is not null) await _auditWriteQueue.StopAsync(CancellationToken.None);
        if (_archiveWriteQueue is not null) await _archiveWriteQueue.StopAsync(CancellationToken.None);

        foreach (var dir in new[] { _dbDirectory, _storageRoot, _sourceRoot })
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }

    private async Task<(Guid DocumentUid, Guid VersionUid)> AddDocumentAsync(string sourcePath)
    {
        var result = await _documentService.AddDocumentAsync(
            sourcePath, Path.GetFileNameWithoutExtension(sourcePath), null, DocumentSourceType.Manual,
            DocumentStorageMode.IndexedInPlace, null, false, CancellationToken.None);
        Assert.True(result.IsSuccess);

        var document = await _documentRepository.GetByUidAsync(result.Value, CancellationToken.None);
        return (result.Value, document!.CurrentVersionId!.Value);
    }

    [Fact]
    public async Task RunOnceAsync_PdfWithTextLayer_MarksDoneAndSearchable()
    {
        var pdfPath = SampleFiles.CreateSimplePdf(_sourceRoot, "نص قابل للفهرسة والبحث الكامل داخل المستند");
        var (_, versionUid) = await AddDocumentAsync(pdfPath);

        await _sweepService.RunOnceAsync(CancellationToken.None);

        var version = await _documentVersionRepository.GetByUidAsync(versionUid, CancellationToken.None);
        Assert.Equal("done", version!.ContentExtractionStatus);
        Assert.True(version.IsSearchablePdf);
    }

    [Fact]
    public async Task RunOnceAsync_ImageOnlyPdf_MarksNeedsOcrAndUpdatesDocumentStatus()
    {
        var pdfPath = SampleFiles.CreateImageOnlyPdf(_sourceRoot);
        var (documentUid, versionUid) = await AddDocumentAsync(pdfPath);

        await _sweepService.RunOnceAsync(CancellationToken.None);

        var version = await _documentVersionRepository.GetByUidAsync(versionUid, CancellationToken.None);
        Assert.Equal("needs_ocr", version!.ContentExtractionStatus);
        Assert.False(version.IsSearchablePdf);

        var document = await _documentRepository.GetByUidAsync(documentUid, CancellationToken.None);
        Assert.Equal(DocumentStatus.NeedsOcr, document!.Status);
    }

    [Fact]
    public async Task RunOnceAsync_UnsupportedExtension_MarksUnsupportedWithoutThrowing()
    {
        var path = Path.Combine(_sourceRoot, "legacy.doc");
        await File.WriteAllTextAsync(path, "pretend legacy word content");
        var (_, versionUid) = await AddDocumentAsync(path);

        await _sweepService.RunOnceAsync(CancellationToken.None);

        var version = await _documentVersionRepository.GetByUidAsync(versionUid, CancellationToken.None);
        Assert.Equal("unsupported", version!.ContentExtractionStatus);
    }

    [Fact]
    public async Task RunOnceAsync_AlreadyProcessedVersion_IsNotReprocessed()
    {
        var pdfPath = SampleFiles.CreateSimplePdf(_sourceRoot, "نص كافٍ لتخطي حد اكتشاف طبقة النص القابلة للقراءة");
        var (_, versionUid) = await AddDocumentAsync(pdfPath);

        await _sweepService.RunOnceAsync(CancellationToken.None);
        File.Delete(pdfPath); // if it were reprocessed, this would surface as a different (missing-file) outcome

        await _sweepService.RunOnceAsync(CancellationToken.None);

        var version = await _documentVersionRepository.GetByUidAsync(versionUid, CancellationToken.None);
        Assert.Equal("done", version!.ContentExtractionStatus);
    }

    [Fact]
    public async Task RunOnceAsync_DocxDocument_ExtractsAndMarksDone()
    {
        var docxPath = SampleFiles.CreateDocx(_sourceRoot, "محتوى مستند وورد");
        var (_, versionUid) = await AddDocumentAsync(docxPath);

        await _sweepService.RunOnceAsync(CancellationToken.None);

        var version = await _documentVersionRepository.GetByUidAsync(versionUid, CancellationToken.None);
        Assert.Equal("done", version!.ContentExtractionStatus);
    }
}
