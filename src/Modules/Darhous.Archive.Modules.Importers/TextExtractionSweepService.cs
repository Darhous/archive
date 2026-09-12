using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Documents;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Modules.Importers;

/// <summary>
/// Same reconciliation-sweep shape as Phase 8's <c>SearchReconciliationService</c> and Phase 9's
/// <c>DiscoveryReconciliationService</c> — a periodic pass over <c>document_versions</c> rows
/// still <c>content_extraction_status='pending'</c>, rather than a dedicated Job type. This
/// avoids a cross-project dependency (Modules.Documents would otherwise need to know about a
/// job type defined in this project just to enqueue it after every AddDocumentAsync call), and
/// a version only needs to be picked up once — no retry/crash-recovery machinery earns its
/// keep here the way it does for Discovery's much longer-running scans.
/// </summary>
public sealed class TextExtractionSweepService(
    IUnitOfWork unitOfWork, IEnumerable<IContentExtractor> extractors, ILogger<TextExtractionSweepService> logger)
    : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(3);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TickInterval);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Text extraction sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Public for direct invocation from tests without waiting on the timer.</summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var pending = await unitOfWork.ExecuteAsync((context, ct) => context.DocumentVersions.ListPendingExtractionAsync(BatchSize, ct), cancellationToken);

        foreach (var version in pending)
        {
            await ExtractOneAsync(version, cancellationToken);
        }
    }

    private async Task ExtractOneAsync(DocumentVersion version, CancellationToken cancellationToken)
    {
        if (!File.Exists(version.FilePath))
        {
            // Discovery's own reconciliation handles Missing-status transitions; extraction just
            // leaves this alone for now rather than guessing at a status that isn't its job to set.
            return;
        }

        var extractor = extractors.FirstOrDefault(e => e.CanHandle(version.FileExtension));
        if (extractor is null)
        {
            await UpdateAsync(version.Uid, ExtractionStatus.Unsupported, null, null, null, cancellationToken);
            return;
        }

        try
        {
            var result = await extractor.ExtractAsync(version.FilePath, cancellationToken);

            if (result.IsSearchablePdf == false)
            {
                // PDF with no text layer — genuinely needs OCR (Phase 15), not an extraction failure.
                await UpdateAsync(version.Uid, ExtractionStatus.NeedsOcr, result.PageCount, false, null, cancellationToken);
                await SetDocumentStatusAsync(version.DocumentUid, DocumentStatus.NeedsOcr, cancellationToken);
                return;
            }

            await UpdateAsync(version.Uid, ExtractionStatus.Done, result.PageCount, result.IsSearchablePdf, result.Text, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Text extraction failed for {FilePath}", version.FilePath);
            await UpdateAsync(version.Uid, ExtractionStatus.Failed, null, null, null, cancellationToken);
            await SetDocumentStatusAsync(version.DocumentUid, DocumentStatus.IndexFailed, cancellationToken);
        }
    }

    private Task UpdateAsync(
        Guid versionUid,
        string status,
        int? pageCount,
        bool? isSearchablePdf,
        string? extractedText,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.DocumentVersions.UpdateExtractionResultAsync(versionUid, status, pageCount, isSearchablePdf, extractedText, ct);
            return null;
        }, cancellationToken);

    private Task SetDocumentStatusAsync(Guid documentUid, DocumentStatus status, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.Documents.SetStatusAsync(documentUid, status, ct);
            return null;
        }, cancellationToken);
}

internal static class ExtractionStatus
{
    public const string Done = "done";
    public const string Failed = "failed";
    public const string NeedsOcr = "needs_ocr";
    public const string Unsupported = "unsupported";
}
