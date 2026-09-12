using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Darhous.Archive.Modules.Discovery;
using Darhous.Archive.Modules.Discovery.WatchFolders;
using Darhous.Archive.Security.Sessions;

namespace Darhous.Archive.Desktop.ViewModels;

/// <summary>
/// SAD §46.9 (approved decision, Phase 2 review): the first-run default is NOT "scan the whole
/// computer" — the user picks starting folders here, with "scan the whole computer" as an
/// explicit, separately-confirmed opt-in. Watch folders default to Indexed In Place (§46.5's
/// stated default for every auto-discovered file); exposing managed copy/move as an onboarding
/// choice is deferred to a future Settings screen, not required for this decision to hold.
/// </summary>
public sealed partial class OnboardingViewModel(
    IWatchFolderService watchFolderService, IDiscoveryOrchestrator discoveryOrchestrator, ArchivePrincipal principal)
    : ObservableObject
{
    public event EventHandler? Completed;

    public ObservableCollection<string> SelectedFolders { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStartScanning))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string? _statusMessage;

    public bool CanStartScanning => !IsBusy && SelectedFolders.Count > 0;

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    public void AddFolder(string path)
    {
        if (!SelectedFolders.Contains(path))
        {
            SelectedFolders.Add(path);
            OnPropertyChanged(nameof(CanStartScanning));
        }
    }

    [RelayCommand]
    private void RemoveFolder(string path)
    {
        SelectedFolders.Remove(path);
        OnPropertyChanged(nameof(CanStartScanning));
    }

    [RelayCommand(CanExecute = nameof(CanStartScanning))]
    private async Task StartScanningAsync()
    {
        IsBusy = true;
        StatusMessage = "جارٍ إضافة المجلدات...";

        foreach (var folder in SelectedFolders)
        {
            var result = await watchFolderService.AddAsync(folder, includeSubfolders: true, "index_in_place", null, principal.UserId, CancellationToken.None);
            if (!result.IsSuccess && result.Error!.Code != "WATCH_FOLDER_ALREADY_ADDED")
            {
                StatusMessage = $"تعذّرت إضافة {folder}: {result.Error.Message}";
                IsBusy = false;
                return;
            }
        }

        var scanResult = await discoveryOrchestrator.StartInitialDiscoveryAsync(principal.UserId, CancellationToken.None);
        IsBusy = false;

        if (!scanResult.IsSuccess)
        {
            StatusMessage = scanResult.Error!.Message;
            return;
        }

        Completed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task ScanFullComputerAsync()
    {
        IsBusy = true;
        StatusMessage = "جارٍ فحص الكمبيوتر بالكامل...";

        var result = await discoveryOrchestrator.StartFullComputerScanAsync(principal.UserId, CancellationToken.None);
        IsBusy = false;

        if (!result.IsSuccess)
        {
            StatusMessage = result.Error!.Message;
            return;
        }

        Completed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Skip() => Completed?.Invoke(this, EventArgs.Empty);
}
