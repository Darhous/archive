using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Documents;
using Darhous.Archive.Core.Jobs;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Modules.Discovery.Jobs;

/// <summary>
/// Implementation Plan §46.6: an Indexed-In-Place document whose file has disappeared gets
/// <c>Status = Missing</c> — the row is never deleted automatically (a human decides what that
/// means: moved drive, renamed, genuinely gone). Only checks the "went missing" direction;
/// automatically un-marking a document Active again if the file reappears is out of scope for
/// V1 (documented gap — a user can still see it's Missing and investigate).
/// </summary>
public sealed class MissingFileReconcileJob(
    IDocumentRepository documentRepository, IDocumentVersionRepository documentVersionRepository,
    IUnitOfWork unitOfWork, ILogger<MissingFileReconcileJob> logger)
    : IBackgroundJob
{
    public string JobType => nameof(MissingFileReconcileJob);

    public async Task ExecuteAsync(IJobContext context, CancellationToken cancellationToken)
    {
        var documents = await documentRepository.ListAsync(cancellationToken);
        var checkedCount = 0;
        var missingCount = 0;

        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (document.DeletedAt is not null
                || document.Status is DocumentStatus.Missing or DocumentStatus.Trashed
                || document.StorageMode != DocumentStorageMode.IndexedInPlace
                || document.CurrentVersionId is not { } versionUid)
            {
                continue;
            }

            var version = await documentVersionRepository.GetByUidAsync(versionUid, cancellationToken);
            if (version is null)
            {
                continue;
            }

            checkedCount++;

            if (File.Exists(version.FilePath))
            {
                continue;
            }

            await unitOfWork.ExecuteAsync<object?>(async (uowContext, ct) =>
            {
                await uowContext.Documents.SetStatusAsync(document.Uid, DocumentStatus.Missing, ct);
                return null;
            }, cancellationToken);

            missingCount++;
        }

        logger.LogInformation("Missing-file reconciliation checked {Checked} Indexed-In-Place documents, {Missing} newly marked Missing", checkedCount, missingCount);
    }
}
