namespace Darhous.Backup.Local;

public sealed record BackupRequest(
    BackupType Type,
    string DestinationDirectory,
    Guid? RequestedBy = null);

public sealed record BackupResult(
    Guid BackupUid,
    BackupType Type,
    string PackagePath,
    long FileSize,
    string Sha256);

public sealed record RestoreResult(
    Guid BackupUid,
    BackupType Type,
    string SafetyBackupPath,
    bool RequiresRestart);

public interface ILocalBackupService
{
    Task<BackupResult> CreateBackupAsync(
        BackupRequest request,
        Func<int, CancellationToken, Task>? reportProgress,
        CancellationToken cancellationToken);

    Task<RestoreResult> RestoreAsync(string packagePath, Guid? requestedBy, CancellationToken cancellationToken);
}

public interface IBackupRequestService
{
    Task<Guid> QueueAsync(BackupRequest request, CancellationToken cancellationToken);
}
