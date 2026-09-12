namespace Darhous.Archive.Modules.Discovery.Tests;

public class ExclusionServiceTests : DiscoveryTestBase
{
    [Fact]
    public async Task SeedTechnicalExclusionsAsync_IsIdempotent()
    {
        await ExclusionService.SeedTechnicalExclusionsAsync(CancellationToken.None);
        await ExclusionService.SeedTechnicalExclusionsAsync(CancellationToken.None);

        var exclusions = await ExclusionService.LoadActiveExclusionsAsync(CancellationToken.None);
        var windowsExcluded = exclusions.IsPathExcluded(Environment.GetFolderPath(Environment.SpecialFolder.Windows));

        Assert.True(windowsExcluded);
    }

    [Fact]
    public async Task LoadActiveExclusionsAsync_ExcludesPathsUnderAUserExclusion()
    {
        var excludedFolder = Path.Combine(ScanRoot, "private");
        Directory.CreateDirectory(excludedFolder);

        await ExclusionService.AddUserExclusionAsync("folder", excludedFolder, "شخصي", null, CancellationToken.None);

        var exclusions = await ExclusionService.LoadActiveExclusionsAsync(CancellationToken.None);

        Assert.True(exclusions.IsPathExcluded(excludedFolder));
        Assert.True(exclusions.IsPathExcluded(Path.Combine(excludedFolder, "nested", "file.pdf")));
        Assert.False(exclusions.IsPathExcluded(ScanRoot));
    }
}
