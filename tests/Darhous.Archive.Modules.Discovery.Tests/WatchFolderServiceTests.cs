namespace Darhous.Archive.Modules.Discovery.Tests;

public class WatchFolderServiceTests : DiscoveryTestBase
{
    [Fact]
    public async Task AddAsync_NonExistentDirectory_Fails()
    {
        var result = await WatchFolderService.AddAsync(
            Path.Combine(ScanRoot, "does-not-exist"), true, "index_in_place", null, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("WATCH_FOLDER_NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task AddAsync_ValidDirectory_SucceedsAndAppearsInEnabledList()
    {
        var result = await WatchFolderService.AddAsync(ScanRoot, true, "index_in_place", null, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var enabled = await WatchFolderService.ListEnabledAsync(CancellationToken.None);
        Assert.Single(enabled);
        Assert.Equal(ScanRoot, enabled[0].Path);
    }

    [Fact]
    public async Task AddAsync_SamePathTwice_SecondCallFails()
    {
        await WatchFolderService.AddAsync(ScanRoot, true, "index_in_place", null, null, CancellationToken.None);
        var second = await WatchFolderService.AddAsync(ScanRoot, true, "index_in_place", null, null, CancellationToken.None);

        Assert.False(second.IsSuccess);
        Assert.Equal("WATCH_FOLDER_ALREADY_ADDED", second.Error!.Code);
    }
}
