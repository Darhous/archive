using System.Windows;
using Darhous.Archive.Security.Authentication;
using Darhous.Archive.Security.Sessions;

namespace Darhous.Archive.Desktop.Views;

public partial class WelcomePlaceholderWindow : Window
{
    private readonly string? _sessionToken;
    private readonly IAuthenticationService _authenticationService;
    private readonly Action _onLogout;

    public WelcomePlaceholderWindow(
        ArchivePrincipal principal, string? sessionToken, IAuthenticationService authenticationService, Action onLogout)
    {
        InitializeComponent();

        _sessionToken = sessionToken;
        _authenticationService = authenticationService;
        _onLogout = onLogout;

        WelcomeText.Text = $"أهلًا، {principal.DisplayName}";
        RoleText.Text = principal.IsGuest ? "وضع الضيف (Guest Mode)" : $"الدور: {principal.Role}";
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
