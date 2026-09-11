namespace Darhous.Archive.Modules.Documents.Storage;

/// <summary>
/// Implementation Plan §38. Ordering that matters for crash safety (Phase 5 design review):
/// the file is moved to its permanent path (<see cref="CommitAsync"/>) BEFORE the caller
/// commits the archive.db transaction that records it. A crash after the file move but
/// before the DB commit only ever leaves an orphaned file (self-healing later — a future
/// integrity check can find and remove it); the reverse ordering could leave a document row
/// pointing at a file that was never actually written, which is worse. A full
/// operation-journal / pending-import table (as a stricter design would use) was judged
/// unnecessary for a single-user desktop app with one serialized writer per database.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Copies <paramref name="sourcePath"/> into a temp staging location under ArchiveStorage.</summary>
    Task<StagedFile> StageAsync(string sourcePath, Guid documentUid, int versionNo, string fileExtension, CancellationToken cancellationToken);

    Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken);

    /// <summary>Moves the staged file to its permanent path. Idempotent: safe to call again if the final file already exists and matches.</summary>
    Task CommitAsync(StagedFile stagedFile, CancellationToken cancellationToken);

    /// <summary>Deletes the temp staging file. Only valid before <see cref="CommitAsync"/>.</summary>
    Task RollbackStagingAsync(StagedFile stagedFile, CancellationToken cancellationToken);

    Task<Stream> OpenReadAsync(string filePath, CancellationToken cancellationToken);

    /// <summary>DB Spec §109 step 2 — moves a managed file out of its live path into internal deletion staging. Returns the staged path.</summary>
    Task<string> MoveToRecycleStagingAsync(string filePath, CancellationToken cancellationToken);

    Task RestoreFromRecycleStagingAsync(string stagedPath, string originalPath, CancellationToken cancellationToken);

    /// <summary>DB Spec §109 step 4 — deletes a file already moved to deletion staging.</summary>
    Task DeletePermanentlyAsync(string stagedPath, CancellationToken cancellationToken);

    bool Exists(string filePath);
}
