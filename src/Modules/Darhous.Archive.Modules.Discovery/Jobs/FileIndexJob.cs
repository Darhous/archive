using System.Text.Json;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Documents;
using Darhous.Archive.Core.Jobs;
using Darhous.Archive.Modules.Documents.Services;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Modules.Discovery.Jobs;

/// <summary>
/// Implementation Plan §53. One discovered file → one document. Duplicate detection by
/// content (SHA-256) is already <see cref="IDocumentService.AddDocumentAsync"/>'s job (built
/// in Phase 5) — Discovery doesn't reimplement it, it just treats "duplicate of an existing
/// document" as a benign skip rather than a job failure, since finding the same file twice on
/// disk isn't an error.
/// </summary>
public sealed class FileIndexJob(
    IDocumentService documentService, IDocumentVersionRepository documentVersionRepository, ILogger<FileIndexJob> logger)
    : IBackgroundJob
{
    public string JobType => nameof(FileIndexJob);

    public async Task ExecuteAsync(IJobContext context, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Deserialize<FileIndexJobPayload>(context.Metadata.PayloadJson ?? "")
            ?? throw new InvalidOperationException("FileIndexJob requires a payload.");

        if (!File.Exists(payload.FilePath))
        {
            logger.LogInformation("Skipping {FilePath} — no longer exists (discovered then deleted before indexing ran)", payload.FilePath);
            return;
        }

        // Re-check right before indexing: two DiscoveryScanJob runs (e.g. a watch folder and an
        // overlapping full-disk scan) can both queue the same path before either indexes it.
        if (await documentVersionRepository.FindByFilePathAsync(payload.FilePath, cancellationToken) is not null)
        {
            return;
        }

        var storageMode = payload.ImportMode switch
        {
            "managed_copy" or "managed_move" => DocumentStorageMode.Managed,
            "index_in_place" => DocumentStorageMode.IndexedInPlace,
            _ => throw new ArgumentOutOfRangeException(nameof(payload), payload.ImportMode, "Unknown import mode."),
        };

        var sourceType = payload.SourceType switch
        {
            "watch_folder" => DocumentSourceType.WatchFolder,
            "import" => DocumentSourceType.Import,
            _ => throw new ArgumentOutOfRangeException(nameof(payload), payload.SourceType, "Unknown source type."),
        };

        var title = Path.GetFileNameWithoutExtension(payload.FilePath);

        var result = await documentService.AddDocumentAsync(
            payload.FilePath, title, payload.DestinationFolderId, sourceType, storageMode,
            payload.RequestedBy, allowDuplicate: false, cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Error!.Code == "DOCUMENT_DUPLICATE_DETECTED")
            {
                logger.LogInformation("Skipping {FilePath} — content already archived under another path", payload.FilePath);
                return;
            }

            throw new InvalidOperationException($"Failed to index {payload.FilePath}: {result.Error.Message}");
        }

        if (payload.ImportMode == "managed_move")
        {
            try
            {
                File.Delete(payload.FilePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The document is already safely copied into ArchiveStorage — failing to delete
                // the original leaves a harmless leftover copy, not a data-integrity problem.
                logger.LogWarning(ex, "Indexed {FilePath} as managed but could not delete the original after move", payload.FilePath);
            }
        }
    }
}
