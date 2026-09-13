using Darhous.Archive.Configuration;
using Darhous.Archive.Desktop.ViewModels.Settings;
using Darhous.Archive.Modules.Ai;
using Darhous.Archive.Security.Secrets;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darhous.Archive.Desktop.Tests;

public class AiSettingsViewModelTests
{
    [Fact]
    public async Task Initialize_ShowsUnacknowledgedWarning_AndKeepsProviderEnablementLocked()
    {
        var registry = new AiProviderRegistry([new UiFakeProvider()]);
        var settingsStore = new InMemoryAppSettingsStore();
        var settings = new AiSettingsService(settingsStore, registry, NullLogger<AiSettingsService>.Instance);
        var viewModel = new AiSettingsViewModel(
            settings,
            new AiSecretStore(settingsStore, new TestSecretProtector()),
            registry);

        await viewModel.InitializeAsync();

        Assert.True(viewModel.ShowPrivacyWarning);
        Assert.False(viewModel.CanEnableProviders);
        Assert.Single(viewModel.Providers);
        Assert.False(viewModel.Providers[0].IsEnabled);
    }

    [Fact]
    public async Task Save_AcknowledgesWarningAndPersistsEnabledActiveProvider()
    {
        var registry = new AiProviderRegistry([new UiFakeProvider()]);
        var settingsStore = new InMemoryAppSettingsStore();
        var settings = new AiSettingsService(settingsStore, registry, NullLogger<AiSettingsService>.Instance);
        var viewModel = new AiSettingsViewModel(
            settings,
            new AiSecretStore(settingsStore, new TestSecretProtector()),
            registry);
        await viewModel.InitializeAsync();
        viewModel.PrivacyAcknowledged = true;
        viewModel.Providers[0].IsEnabled = true;
        viewModel.SelectedProvider = viewModel.Providers[0];

        await viewModel.SaveCommand.ExecuteAsync(null);

        var persisted = await settings.GetAsync(CancellationToken.None);
        Assert.True(persisted.PrivacyAcknowledged);
        Assert.True(persisted.IsProviderEnabled("ui-fake"));
        Assert.Equal("ui-fake", persisted.ActiveProviderId);
    }

    private sealed class UiFakeProvider : IAiProvider
    {
        public string ProviderId => "ui-fake";

        public Task<IReadOnlyList<AiModelInfo>> ListModelsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AiModelInfo>>([]);

        public Task<AiResponse> ExecuteAsync(AiRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestSecretProtector : ISecretProtector
    {
        public string Protect(string plaintext) => plaintext;

        public string Unprotect(string protectedValue) => protectedValue;
    }
}
