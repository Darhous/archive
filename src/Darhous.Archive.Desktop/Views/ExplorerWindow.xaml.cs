using System.Windows;
using System.Windows.Controls;
using Darhous.Archive.Desktop.ViewModels.Explorer;
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
    }

    private void FolderTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FolderNodeViewModel node)
        {
            _viewModel.SelectedFolder = node;
        }
    }

    private void DocumentListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
