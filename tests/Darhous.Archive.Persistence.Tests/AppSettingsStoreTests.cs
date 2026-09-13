using Darhous.Archive.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Darhous.Archive.Persistence.Tests;

public sealed class AppSettingsStoreTests : PersistenceTestBase
{
    [Fact]
    public async Task SetAsync_PersistsAcrossStoreInstances()
    {
        await using var first = await CreateProviderAsync();
        var store = first.GetRequiredService<IAppSettingsStore>();
        await store.SetAsync("updates.auto_check", "true", CancellationToken.None);

        Assert.Equal("true", await store.GetAsync("updates.auto_check", CancellationToken.None));

        await using var second = await CreateProviderAsync();
        Assert.Equal(
            "true",
            await second.GetRequiredService<IAppSettingsStore>()
                .GetAsync("updates.auto_check", CancellationToken.None));
    }

    private async Task<ServiceProvider> CreateProviderAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Core.Time.IClock, Core.Time.SystemClock>();
        services.AddPersistence(Options);
        var provider = services.BuildServiceProvider();
        foreach (var hosted in provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>())
        {
            await hosted.StartAsync(CancellationToken.None);
        }

        return provider;
    }
}
