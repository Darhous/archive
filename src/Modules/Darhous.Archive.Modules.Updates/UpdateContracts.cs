namespace Darhous.Archive.Modules.Updates;

public sealed record UpdateCandidate(
    string ComponentType,
    string ComponentId,
    Version Version,
    string PackagePath,
    bool IncludesMigration);

public sealed record UpdateCheckResult(
    string ComponentType,
    string ComponentId,
    Version InstalledVersion,
    UpdateCandidate? Latest,
    bool IsUpdateAvailable);

public sealed record UpdateInstallResult(
    long HistoryId,
    Version FromVersion,
    Version ToVersion,
    string RollbackPointPath,
    string? SafetyBackupPath,
    bool RequiresRestart);

public sealed record UpdateUserSettings(bool AutoCheck = true, bool AutoInstall = false);

public interface IUpdateSource
{
    Task<UpdateCandidate?> GetLatestAsync(
        string componentType,
        string componentId,
        CancellationToken cancellationToken);
}

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken);

    Task<UpdateInstallResult> InstallAsync(
        string packagePath,
        Guid? initiatedBy,
        Func<int, CancellationToken, Task>? reportProgress,
        CancellationToken cancellationToken);

    Task RollbackAsync(
        long historyId,
        Guid? initiatedBy,
        Func<int, CancellationToken, Task>? reportProgress,
        CancellationToken cancellationToken);
}

public interface IUpdateRequestService
{
    Task<Guid> QueueInstallAsync(string packagePath, Guid? initiatedBy, CancellationToken cancellationToken);

    Task<Guid> QueueLatestRollbackAsync(Guid? initiatedBy, CancellationToken cancellationToken);
}

public interface IUpdateSettingsService
{
    Task<UpdateUserSettings> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(UpdateUserSettings settings, CancellationToken cancellationToken);
}
