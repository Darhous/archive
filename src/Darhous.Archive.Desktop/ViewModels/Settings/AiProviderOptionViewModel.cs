using CommunityToolkit.Mvvm.ComponentModel;

namespace Darhous.Archive.Desktop.ViewModels.Settings;

public sealed partial class AiProviderOptionViewModel(string providerId, bool isEnabled) : ObservableObject
{
    public string ProviderId { get; } = providerId;

    [ObservableProperty]
    private bool _isEnabled = isEnabled;
}
