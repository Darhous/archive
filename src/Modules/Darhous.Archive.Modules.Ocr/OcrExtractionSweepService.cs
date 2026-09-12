using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Documents;
using Darhous.Archive.Workers.Protocol;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Modules.Ocr;

/// <summary>
/// Reconciliation sweep matching Phase 10's extraction service: every pass processes the
/// oldest versions already marked <c>needs_ocr</c>, without creating a cross-module Job type.
/// </summary>
public sealed class OcrExtractionSweepService(
    IUnitOfWork unitOfWork,
    IOcrProvider ocrProvider,
    OcrSupervisionOptions options,
    ILogger<OcrExtractionSweepService> logger) : BackgroundService
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
                logger.LogError(ex, "OCR extraction sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var pending = await unitOfWork.ExecuteAsync(
            (context, ct) => context.DocumentVersions.ListNeedingOcrAsync(BatchSize, ct),
            cancellationToken);
        foreach (var version in pending)
        {
            await ProcessOneAsync(version, cancellationToken);
        }
    }

    private async Task ProcessOneAsync(DocumentVersion version, CancellationToken cancellationToken)
    {
        if (!File.Exists(version.FilePath)) return;

        OcrResult result;
        try
        {
            result = await ocrProvider.ProcessAsync(version.FilePath, options.DefaultLanguages, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "OCR provider failed for {FilePath}", version.FilePath);
            await MarkFailedAsync(version, cancellationToken);
            return;
        }

        if (!result.IsSuccess || result.ExtractedText is null || result.SearchableFilePath is null)
        {
            logger.LogWarning(
                "OCR failed for {FilePath}: {FailureCode} {FailureMessage}",
                version.FilePath,
                result.FailureCode,
                result.FailureMessage);
            await MarkFailedAsync(version, cancellationToken);
            return;
        }

        string? derivedPath = null;
        try
        {
            derivedPath = await CopyToArchiveStorageAsync(version, result.SearchableFilePath, cancellationToken);
            await unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
            {
                await context.Documents.SetStatusAsync(version.DocumentUid, DocumentStatus.Active, ct);
                await context.DocumentVersions.UpdateOcrResultAsync(
                    version.Uid,
                    result.ExtractedText,
                    derivedPath,
                    "Tesseract",
                    ct);
                return null;
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not persist OCR result for {FilePath}", version.FilePath);
            if (derivedPath is not null) TryDeleteFile(derivedPath);
            await MarkFailedAsync(version, cancellationToken);
        }
        finally
        {
            TryDeleteManagedOutput(result.SearchableFilePath);
        }
    }

    private async Task<string> CopyToArchiveStorageAsync(
        DocumentVersion version,
        string temporaryPdf,
        CancellationToken cancellationToken)
    {
        var validator = new ManagedTempPathValidator(options.Protocol);
        var source = validator.ValidateAndNormalize(temporaryPdf);
        if (!File.Exists(source) || new FileInfo(source).Length == 0)
        {
            throw new IOException("OCR provider output is missing or empty.");
        }

        var destinationDirectory = Path.Combine(
            options.ArchiveStorageRoot,
            "Derived",
            "Ocr",
            version.DocumentUid.ToString("N"));
        Directory.CreateDirectory(destinationDirectory);
        var destination = Path.Combine(destinationDirectory, $"{version.Uid:N}-{Guid.NewGuid():N}.pdf");
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        await input.CopyToAsync(output, cancellationToken);
        await output.FlushAsync(cancellationToken);
        return destination;
    }

    private Task MarkFailedAsync(DocumentVersion version, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.Documents.SetStatusAsync(version.DocumentUid, DocumentStatus.IndexFailed, ct);
            await context.DocumentVersions.UpdateExtractionResultAsync(
                version.Uid,
                "failed",
                version.PageCount,
                false,
                null,
                ct);
            return null;
        }, cancellationToken);

    private void TryDeleteManagedOutput(string path)
    {
        try
        {
            var validator = new ManagedTempPathValidator(options.Protocol);
            var normalized = validator.ValidateAndNormalize(path);
            var directory = Path.GetDirectoryName(normalized);
            if (directory is not null && Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or WorkerProtocolException)
        {
            logger.LogWarning(ex, "Could not clean OCR temporary output {Path}", path);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
