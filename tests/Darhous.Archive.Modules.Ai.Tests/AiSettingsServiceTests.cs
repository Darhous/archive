using Darhous.Archive.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darhous.Archive.Modules.Ai.Tests;

public class AiSettingsServiceTests
{
    private readonly InMemoryAppSettingsStore _store = new();
    private readonly AiProviderRegistry _registry = new([new FakeAiProvider("synthetic-provider")]);

    [Fact]
    public async Task MissingSettings_DefaultsToCloudAiDisabled()
    {
        var service = CreateService();

        var settings = await service.GetAsync(CancellationToken.None);

        Assert.False(settings.PrivacyAcknowledged);
        Assert.Null(settings.ActiveProviderId);
        Assert.Empty(settings.ProviderEnabled);
    }

    [Fact]
    public async Task EnablingProviderWithoutPrivacyAcknowledgment_IsRejectedByService()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<AiPrivacyAcknowledgmentRequiredException>(() =>
            service.SetProviderEnabledAsync("synthetic-provider", true, CancellationToken.None));

        Assert.False((await service.GetAsync(CancellationToken.None)).IsProviderEnabled("synthetic-provider"));
    }

    [Fact]
    public async Task AcknowledgedProvider_CanBeEnabledAndSelected_AndPersists()
    {
        var service = CreateService();
        await service.SetPrivacyAcknowledgedAsync(true, CancellationToken.None);
        await service.SetProviderEnabledAsync("synthetic-provider", true, CancellationToken.None);
        await service.SetActiveProviderAsync("synthetic-provider", CancellationToken.None);

        var reloaded = CreateService();
        var settings = await reloaded.GetAsync(CancellationToken.None);

        Assert.True(settings.PrivacyAcknowledged);
        Assert.True(settings.IsProviderEnabled("SYNTHETIC-PROVIDER"));
        Assert.Equal("synthetic-provider", settings.ActiveProviderId);
    }

    [Fact]
    public async Task RevokingPrivacyAcknowledgment_DisablesAndDeactivatesAllProviders()
    {
        var service = CreateService();
        await service.SetPrivacyAcknowledgedAsync(true, CancellationToken.None);
        await service.SetProviderEnabledAsync("synthetic-provider", true, CancellationToken.None);
        await service.SetActiveProviderAsync("synthetic-provider", CancellationToken.None);

        await service.SetPrivacyAcknowledgedAsync(false, CancellationToken.None);
        var settings = await service.GetAsync(CancellationToken.None);

        Assert.False(settings.PrivacyAcknowledged);
        Assert.Null(settings.ActiveProviderId);
        Assert.Empty(settings.ProviderEnabled);
    }

    [Fact]
    public async Task EnablingUnknownProvider_IsRejectedAfterAcknowledgment()
    {
        var service = CreateService();
        await service.SetPrivacyAcknowledgedAsync(true, CancellationToken.None);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.SetProviderEnabledAsync("not-installed", true, CancellationToken.None));
    }

    private AiSettingsService CreateService() =>
        new(_store, _registry, NullLogger<AiSettingsService>.Instance);
}
