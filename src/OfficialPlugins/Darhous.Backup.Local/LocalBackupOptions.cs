using Darhous.Archive.Configuration;

namespace Darhous.Backup.Local;

public sealed class LocalBackupOptions
{
    public string ManagedFilesDirectory { get; init; } = AppPaths.ArchiveStorage;
    public string SettingsDirectory { get; init; } = AppPaths.UserSettingsRoot;
    public string SafetyBackupDirectory { get; init; } = AppPaths.Backups;
    public string WorkingDirectory { get; init; } = AppPaths.Temp;
    public Version ApplicationVersion { get; init; } = new(1, 0, 0);
}
