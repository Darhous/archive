using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Darhous.Archive.Configuration;
using Darhous.Archive.Security.Authentication;
using Darhous.Archive.Security.Sessions;

namespace Darhous.Archive.Desktop.ViewModels;

/// <summary>UI/UX Design System §91 (Login Screen) — minimal: username, password, remember me, login, guest.</summary>
public sealed partial class LoginViewModel : ObservableObject
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IAppSettingsStore _appSettingsStore;

    public event EventHandler<LoginSucceededEventArgs>? LoginSucceeded;

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private bool _rememberMe;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _guestModeAvailable;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool CanSubmit => !IsBusy;

    public LoginViewModel(IAuthenticationService authenticationService, IAppSettingsStore appSettingsStore)
    {
        _authenticationService = authenticationService;
        _appSettingsStore = appSettingsStore;
    }

    public async Task InitializeAsync()
    {
        var guestEnabled = await _appSettingsStore.GetAsync("guest.enabled", CancellationToken.None);
        GuestModeAvailable = guestEnabled == "true";
    }

    [RelayCommand]
    private async Task LoginAsync(string? password)
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrEmpty(password))
        {
            ErrorMessage = "من فضلك أدخل اسم المستخدم وكلمة المرور.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await _authenticationService.LoginAsync(Username.Trim(), password, RememberMe, CancellationToken.None);

            if (result.IsFailure)
            {
                ErrorMessage = result.Error!.Message;
                return;
            }

            LoginSucceeded?.Invoke(this, new LoginSucceededEventArgs(result.Value.Principal, result.Value.SessionToken));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ContinueAsGuest()
    {
        if (!GuestModeAvailable)
        {
            return;
        }

        LoginSucceeded?.Invoke(this, new LoginSucceededEventArgs(ArchivePrincipal.Guest, sessionToken: null));
    }
}

public sealed class LoginSucceededEventArgs(ArchivePrincipal principal, string? sessionToken) : EventArgs
{
    public ArchivePrincipal Principal { get; } = principal;

    public string? SessionToken { get; } = sessionToken;
}
