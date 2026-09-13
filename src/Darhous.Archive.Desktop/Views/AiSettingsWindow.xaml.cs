using System.Windows;
using Darhous.Archive.Desktop.ViewModels.Settings;

namespace Darhous.Archive.Desktop.Views;

public partial class AiSettingsWindow : Window
{
    private readonly AiSettingsViewModel _viewModel;

    public AiSettingsWindow(AiSettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }

    private async void SaveApiKey_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SaveApiKeyAsync(ApiKeyBox.Password);
        ApiKeyBox.Clear();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
