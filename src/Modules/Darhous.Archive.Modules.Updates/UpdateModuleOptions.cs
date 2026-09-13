using Darhous.Archive.Configuration;

namespace Darhous.Archive.Modules.Updates;

public sealed class UpdateModuleOptions
{
    public const string ApplicationComponentType = "application";
    public const string ApplicationComponentId = "Darhous.Archive.Desktop";

    public string ComponentType { get; init; } = ApplicationComponentType;
    public string ComponentId { get; init; } = ApplicationComponentId;
    public Version InstalledVersion { get; init; } = new(1, 0, 0);
    public string InstallDirectory { get; init; } = AppContext.BaseDirectory;
    public string FeedManifestPath { get; init; } = Path.Combine(AppPaths.Updates, "feed.json");
    public string StagingDirectory { get; init; } = Path.Combine(AppPaths.Updates, "Staging");
    public string RollbackDirectory { get; init; } = Path.Combine(AppPaths.Updates, "Rollback");
    public string ActiveVersionFilePath { get; init; } = Path.Combine(AppPaths.Updates, "active-version.txt");
    public string SafetyBackupDirectory { get; init; } = AppPaths.Backups;
    public long MaximumPackageBytes { get; init; } = 2L * 1024 * 1024 * 1024;
    public long MaximumExtractedBytes { get; init; } = 4L * 1024 * 1024 * 1024;
}
