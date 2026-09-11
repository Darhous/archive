namespace Darhous.Archive.Configuration.Tests;

public class InMemoryAppSettingsStoreTests
{
    [Fact]
    public async Task GetAsync_UnknownKey_ReturnsNull()
    {
        var store = new InMemoryAppSettingsStore();

        Assert.Null(await store.GetAsync("missing", CancellationToken.None));
    }

    [Fact]
    public async Task SetThenGet_RoundTrips()
    {
        var store = new InMemoryAppSettingsStore();

        await store.SetAsync("theme", "dark", CancellationToken.None);

        Assert.Equal("dark", await store.GetAsync("theme", CancellationToken.None));
    }
}
