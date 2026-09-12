using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Darhous.Archive.Desktop.ViewModels.Explorer;
using Darhous.Archive.Desktop.ViewModels.Explorer.Preview;
using Darhous.Archive.Security.Authentication;
using Darhous.Archive.Security.Sessions;

namespace Darhous.Archive.Desktop.Views;

public partial class ExplorerWindow : Window
{
    private readonly ExplorerViewModel _viewModel;
    private readonly string? _sessionToken;
    private readonly IAuthenticationService _authenticationService;
    private readonly Action _onLogout;

    public ExplorerWindow(
        ExplorerViewModel viewModel, ArchivePrincipal principal, string? sessionToken,
        IAuthenticationService authenticationService, Action onLogout)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _sessionToken = sessionToken;
        _authenticationService = authenticationService;
        _onLogout = onLogout;

        DataContext = viewModel;
        UserText.Text = principal.IsGuest ? "وضع الضيف (Guest Mode)" : $"{principal.DisplayName} — {principal.Role}";

        Loaded += async (_, _) => await viewModel.InitializeAsync();
        viewModel.StatusMessage += (_, message) => Title = $"Darhous Smart Archive — {message}";
        viewModel.Preview.PropertyChanged += Preview_PropertyChanged;
    }

    /// <summary>
    /// WebView2 navigation is driven imperatively rather than through XAML binding — its
    /// Source setter needs CoreWebView2 initialized first, and navigating on every unrelated
    /// property change (e.g. Status) would reload the same PDF repeatedly. Only FilePath/Kind
    /// actually changing to a PDF should trigger a navigation.
    /// </summary>
    private async void Preview_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(PreviewViewModel.FilePath) or nameof(PreviewViewModel.Kind)))
        {
            return;
        }

        if (sender is not PreviewViewModel preview || preview.Kind != PreviewKind.Pdf || preview.FilePath is null)
        {
            return;
        }

        await PdfPreview.EnsureCoreWebView2Async();
        PdfPreview.Source = new Uri(preview.FilePath);
    }

    private async void PreviewTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != PreviewTabs)
        {
            return; // ListView selection inside a tab also raises SelectionChanged — only react to the TabControl's own event.
        }

        if (PreviewTabs.SelectedItem == VersionsTab)
        {
            await _viewModel.Preview.LoadVersionsCommand.ExecuteAsync(null);
        }
        else if (PreviewTabs.SelectedItem == ActivityTab)
        {
            await _viewModel.Preview.LoadActivityCommand.ExecuteAsync(null);
        }
    }

    private void FolderTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FolderNodeViewModel node)
        {
            _viewModel.SelectedFolder = node;
        }
    }

    private async void DocumentListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        foreach (DocumentRowViewModel row in e.RemovedItems)
        {
            row.IsSelected = false;
        }

        foreach (DocumentRowViewModel row in e.AddedItems)
        {
            row.IsSelected = true;
        }

        _viewModel.UpdateSelectionCount();
        await _viewModel.UpdatePreviewForSelectionAsync();
    }

    private async void Logout_Click(object sender, RoutedEventArgs e)
    {
        if (_sessionToken is not null)
        {
            await _authenticationService.LogoutAsync(_sessionToken, CancellationToken.None);
        }

        _onLogout();
        Close();
    }
}
