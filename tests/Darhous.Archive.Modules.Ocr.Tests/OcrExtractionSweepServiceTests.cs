using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Audit.Writing;
using Darhous.Archive.Contracts.Documents;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Modules.Documents.Services;
using Darhous.Archive.Modules.Documents.Storage;
using Darhous.Archive.Persistence;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Repositories;
using Darhous.Archive.Persistence.Transactions;
using Darhous.Archive.Persistence.Writes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darhous.Archive.Modules.Ocr.Tests;

public sealed class OcrExtractionSweepServiceTests : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Darhous.Ocr.Sweep.Tests", Guid.NewGuid().ToString("N"));
    private SqliteWriteQueue? _archiveQueue;
    private SqliteWriteQueue? _auditQueue;
    private BufferedAuditService? _auditService;
    private SqliteUnitOfWork _unitOfWork = null!;
    private DocumentService _documentService = null!;
    private DocumentRepository _documents = null!;
    private DocumentVersionRepository _versions = null!;
    private OcrSupervisionOptions _options = null!;

    public async Task InitializeAsync()
    {
        var data = Path.Combine(_root, "data");
        var archive = Path.Combine(_root, "archive");
        var temp = Path.Combine(_root, "temp");
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(archive);
        Directory.CreateDirectory(temp);

        var persistenceOptions = new PersistenceOptions { DataDirectory = data };
        await PersistenceInitializer.InitializeAsync(persistenceOptions, CancellationToken.None);
        var connections = new SqliteConnectionFactory(persistenceOptions);
        _archiveQueue = new SqliteWriteQueue(DatabaseKind.Archive, connections, NullLogger<SqliteWriteQueue>.Instance);
        _auditQueue = new SqliteWriteQueue(DatabaseKind.Audit, connections, NullLogger<SqliteWriteQueue>.Instance);
        await _archiveQueue.StartAsync(CancellationToken.None);
        await _auditQueue.StartAsync(CancellationToken.None);
        _auditService = new BufferedAuditService(_auditQueue, connections, new SystemClock(), NullLogger<BufferedAuditService>.Instance);
        await _auditService.StartAsync(CancellationToken.None);

        _unitOfWork = new SqliteUnitOfWork(_archiveQueue);
        _documentService = new DocumentService(
            _unitOfWork,
            new FileStorageService(new DocumentStorageOptions { ArchiveStorageRoot = archive }),
            new SystemClock(),
            _auditService,
            NullLogger<DocumentService>.Instance);
        _documents = new DocumentRepository(connections);
        _versions = new DocumentVersionRepository(connections);
        _options = new OcrSupervisionOptions
        {
            ArchiveStorageRoot = archive,
            Protocol = new() { ManagedTempStorageRoot = temp },
        };
    }

    public async Task DisposeAsync()
    {
        if (_auditService is not null) await _auditService.StopAsync(CancellationToken.None);
        if (_auditQueue is not null) await _auditQueue.StopAsync(CancellationToken.None);
        if (_archiveQueue is not null) await _archiveQueue.StopAsync(CancellationToken.None);
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task Success_PersistsTextDerivedPathAndActiveStatusWithoutChangingOriginal()
    {
        var source = Path.Combine(_root, "original.pdf");
        const string originalBytes = "%PDF-1.4 immutable original";
        await File.WriteAllTextAsync(source, originalBytes);
        var (documentUid, versionUid) = await AddNeedsOcrDocumentAsync(source);
        var provider = new FakeProvider(_options.Protocol.ManagedTempStorageRoot);
        var sweep = CreateSweep(provider);

        await sweep.RunOnceAsync(CancellationToken.None);

        var version = await _versions.GetByUidAsync(versionUid, CancellationToken.None);
        Assert.NotNull(version);
        Assert.Equal("done", version.ContentExtractionStatus);
        Assert.Equal("النص المستخرج داخل الصفحة", version.ExtractedText);
        Assert.True(version.IsSearchablePdf);
        Assert.Equal("Tesseract", version.OcrProvider);
        Assert.NotNull(version.SearchableFilePath);
        Assert.StartsWith(Path.GetFullPath(_options.ArchiveStorageRoot), Path.GetFullPath(version.SearchableFilePath), StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(version.SearchableFilePath));
        Assert.Equal(originalBytes, await File.ReadAllTextAsync(source));
        Assert.Equal(DocumentStatus.Active, (await _documents.GetByUidAsync(documentUid, CancellationToken.None))!.Status);
        Assert.Equal(["ara"], provider.LastLanguages);
    }

    [Fact]
    public async Task Failure_MarksVersionFailedAndDocumentIndexFailed()
    {
        var source = Path.Combine(_root, "failed.pdf");
        await File.WriteAllTextAsync(source, "%PDF-1.4 input");
        var (documentUid, versionUid) = await AddNeedsOcrDocumentAsync(source);
        var provider = new FakeProvider(_options.Protocol.ManagedTempStorageRoot)
        {
            Result = OcrResult.Failed("ocr_engine_unavailable", "No trained data."),
        };

        await CreateSweep(provider).RunOnceAsync(CancellationToken.None);

        Assert.Equal("failed", (await _versions.GetByUidAsync(versionUid, CancellationToken.None))!.ContentExtractionStatus);
        Assert.Equal(DocumentStatus.IndexFailed, (await _documents.GetByUidAsync(documentUid, CancellationToken.None))!.Status);
    }

    private OcrExtractionSweepService CreateSweep(IOcrProvider provider) =>
        new(_unitOfWork, provider, _options, NullLogger<OcrExtractionSweepService>.Instance);

    private async Task<(Guid DocumentUid, Guid VersionUid)> AddNeedsOcrDocumentAsync(string source)
    {
        var result = await _documentService.AddDocumentAsync(
            source,
            "OCR candidate",
            null,
            DocumentSourceType.Manual,
            DocumentStorageMode.IndexedInPlace,
            null,
            false,
            CancellationToken.None);
        Assert.True(result.IsSuccess);
        var document = await _documents.GetByUidAsync(result.Value, CancellationToken.None);
        var versionUid = document!.CurrentVersionId!.Value;
        await _unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.DocumentVersions.UpdateExtractionResultAsync(versionUid, "needs_ocr", 1, false, null, ct);
            await context.Documents.SetStatusAsync(result.Value, DocumentStatus.NeedsOcr, ct);
            return null;
        }, CancellationToken.None);
        return (result.Value, versionUid);
    }

    private sealed class FakeProvider(string tempRoot) : IOcrProvider
    {
        public OcrResult? Result { get; init; }
        public IReadOnlyList<string>? LastLanguages { get; private set; }

        public async Task<OcrResult> ProcessAsync(
            string inputFilePath,
            IReadOnlyList<string>? languages = null,
            CancellationToken cancellationToken = default)
        {
            LastLanguages = languages?.ToArray();
            if (Result is not null) return Result;
            var directory = Path.Combine(tempRoot, $"fake-output-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var output = Path.Combine(directory, "searchable.pdf");
            await File.WriteAllTextAsync(output, "%PDF-1.4 searchable derived", cancellationToken);
            return OcrResult.Succeeded("النص المستخرج داخل الصفحة", output);
        }
    }
}
