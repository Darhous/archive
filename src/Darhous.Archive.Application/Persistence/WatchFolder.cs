namespace Darhous.Archive.Application.Persistence;

/// <summary>DB Spec §58 (watch_folders) — SAD §46.9's Onboarding scope: what Discovery scans by default instead of "the whole computer".</summary>
public sealed record WatchFolder(
    Guid Uid,
    string Path,
    bool IncludeSubfolders,
    string ImportMode,
    Guid? DestinationFolderId,
    bool IsEnabled,
    DateTimeOffset? LastReconciledAt,
    Guid? CreatedBy,
    DateTimeOffset CreatedAt);

public sealed record NewWatchFolder(string Path, bool IncludeSubfolders, string ImportMode, Guid? DestinationFolderId, Guid? CreatedBy);
