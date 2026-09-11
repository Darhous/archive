namespace Darhous.Archive.Configuration.Tests;

public class AppPathsTests
{
    [Fact]
    public void AllPaths_AreUnderProgramDataRoot()
    {
        string[] paths =
        [
            AppPaths.Data, AppPaths.Logs, AppPaths.Plugins, AppPaths.PluginData,
            AppPaths.ArchiveStorage, AppPaths.Backups, AppPaths.Temp, AppPaths.Quarantine, AppPaths.Updates,
        ];

        Assert.All(paths, p => Assert.StartsWith(AppPaths.ProgramDataRoot, p));
    }

    [Fact]
    public void ProgramDataRoot_EndsWithDarhousSmartArchive()
    {
        Assert.EndsWith("DarhousSmartArchive", AppPaths.ProgramDataRoot);
    }

    [Fact]
    public void UserSettingsRoot_IsUnderLocalAppData()
    {
        var expectedRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.StartsWith(expectedRoot, AppPaths.UserSettingsRoot);
    }
}
