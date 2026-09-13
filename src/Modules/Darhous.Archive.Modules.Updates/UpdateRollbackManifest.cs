namespace Darhous.Archive.Modules.Updates;

internal sealed record UpdateRollbackManifest(
    long HistoryId,
    string ComponentType,
    string ComponentId,
    string PreviousVersion,
    string InstalledVersion,
    string? SafetyBackupPath,
    IReadOnlyList<UpdateRollbackFile> Files);

internal sealed record UpdateRollbackFile(string RelativePath, bool HadOriginal, string? Sha256);
