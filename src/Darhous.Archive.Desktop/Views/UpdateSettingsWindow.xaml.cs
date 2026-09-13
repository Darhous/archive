using System.Windows;
using Microsoft.Win32;
using Darhous.Archive.Modules.Updates;

namespace Darhous.Archive.Desktop.Views;

public partial class UpdateSettingsWindow : Window
{
    private readonly IUpdateService _updateService;
    private readonly IUpdateSettingsService _settingsService;
    private readonly IUpdateRequestService _requestService;
    private readonly Guid? _initiatedBy;

    public UpdateSettingsWindow(
        IUpdateService updateService,
        IUpdateSettingsService settingsService,
        IUpdateRequestService requestService,
        Guid? initiatedBy)
    {
        InitializeComponent();
        _updateService = updateService;
        _settingsService = settingsService;
        _requestService = requestService;
        _initiatedBy = initiatedBy;
        Loaded += LoadSettingsAsync;
    }

    private async void LoadSettingsAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            var settings = await _settingsService.GetAsync(CancellationToken.None);
            AutoCheckBox.IsChecked = settings.AutoCheck;
            AutoInstallBox.IsChecked = settings.AutoInstall;
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private async void Check_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "Checking...";
            var result = await _updateService.CheckAsync(CancellationToken.None);
            StatusText.Text = result.IsUpdateAvailable
                ? $"Version {result.Latest!.Version} is available."
                : result.Latest is null
                    ? "No local update feed is configured."
                    : $"You already have the latest version ({result.InstalledVersion}).";
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a signed Darhous update package",
            Filter = "Darhous update (*.darhousupdate)|*.darhousupdate|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var jobUid = await _requestService.QueueInstallAsync(
                dialog.FileName, _initiatedBy, CancellationToken.None);
            StatusText.Text = $"Update install queued ({jobUid}).";
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private async void Rollback_Click(object sender, RoutedEventArgs e)
    {
        var confirmation = MessageBox.Show(
            this,
            "Queue rollback of the latest completed update? The application must restart afterward.",
            "Confirm rollback",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var jobUid = await _requestService.QueueLatestRollbackAsync(_initiatedBy, CancellationToken.None);
            StatusText.Text = $"Rollback queued ({jobUid}).";
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _settingsService.SaveAsync(
                new UpdateUserSettings(AutoCheckBox.IsChecked == true, AutoInstallBox.IsChecked == true),
                CancellationToken.None);
            StatusText.Text = "Update settings saved.";
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ShowError(Exception exception)
    {
        StatusText.Text = exception.Message;
        MessageBox.Show(this, exception.Message, "Updates", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
