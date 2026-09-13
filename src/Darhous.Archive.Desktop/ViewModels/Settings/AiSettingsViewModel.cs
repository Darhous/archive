using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Darhous.Archive.Modules.Ai;

namespace Darhous.Archive.Desktop.ViewModels.Settings;

public sealed partial class AiSettingsViewModel(
    IAiSettingsService settingsService,
    IAiSecretStore secretStore,
    IAiProviderRegistry providerRegistry) : ObservableObject
{
    public ObservableCollection<AiProviderOptionViewModel> Providers { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPrivacyWarning))]
    [NotifyPropertyChangedFor(nameof(CanEnableProviders))]
    private bool _privacyAcknowledged;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSaveApiKey))]
    private AiProviderOptionViewModel? _selectedProvider;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEnableProviders))]
    [NotifyPropertyChangedFor(nameof(CanSaveApiKey))]
    private bool _isBusy;

    public bool ShowPrivacyWarning => !PrivacyAcknowledged;

    public bool CanEnableProviders => PrivacyAcknowledged && !IsBusy;

    public bool CanSaveApiKey => SelectedProvider is not null && !IsBusy;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            var settings = await settingsService.GetAsync(cancellationToken);
            PrivacyAcknowledged = settings.PrivacyAcknowledged;
            Providers.Clear();
            foreach (var providerId in providerRegistry.ProviderIds)
            {
                var option = new AiProviderOptionViewModel(providerId, settings.IsProviderEnabled(providerId));
                Providers.Add(option);
                if (string.Equals(providerId, settings.ActiveProviderId, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedProvider = option;
                }
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            await settingsService.SetPrivacyAcknowledgedAsync(PrivacyAcknowledged, CancellationToken.None);
            if (PrivacyAcknowledged)
            {
                foreach (var provider in Providers)
                {
                    await settingsService.SetProviderEnabledAsync(
                        provider.ProviderId,
                        provider.IsEnabled,
                        CancellationToken.None);
                }

                await settingsService.SetActiveProviderAsync(
                    SelectedProvider is { IsEnabled: true } ? SelectedProvider.ProviderId : null,
                    CancellationToken.None);
            }
            else
            {
                foreach (var provider in Providers)
                {
                    provider.IsEnabled = false;
                }
                SelectedProvider = null;
            }

            StatusMessage = "تم حفظ إعدادات الذكاء الاصطناعي.";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SaveApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (SelectedProvider is null)
        {
            StatusMessage = "اختر موفرًا أولًا.";
            return;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            StatusMessage = "أدخل مفتاح API غير فارغ.";
            return;
        }

        try
        {
            await secretStore.SetApiKeyAsync(SelectedProvider.ProviderId, apiKey, cancellationToken);
            StatusMessage = "تم حفظ المفتاح مشفرًا بواسطة Windows DPAPI.";
        }
        catch (Exception exception)
        {
            StatusMessage = exception.Message;
        }
    }
}
