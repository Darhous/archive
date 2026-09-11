namespace Darhous.Archive.Security.Tests;

public class AuthenticationServiceTests : AuthenticationTestBase
{
    [Fact]
    public async Task LoginAsync_CorrectCredentials_Succeeds()
    {
        await CreateUserAsync("ahmed", "correct-horse-battery-staple");

        var result = await AuthenticationService.LoginAsync("ahmed", "correct-horse-battery-staple", rememberMe: false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrEmpty(result.Value.SessionToken));
        Assert.Equal("ahmed", result.Value.Principal.DisplayName);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_Fails_WithGenericError()
    {
        await CreateUserAsync("ahmed", "correct-password");

        var result = await AuthenticationService.LoginAsync("ahmed", "wrong-password", rememberMe: false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("AUTH_INVALID_CREDENTIALS", result.Error!.Code);
    }

    [Fact]
    public async Task LoginAsync_UnknownUsername_FailsWithSameErrorAsWrongPassword()
    {
        var result = await AuthenticationService.LoginAsync("nobody", "whatever", rememberMe: false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("AUTH_INVALID_CREDENTIALS", result.Error!.Code);
    }

    [Fact]
    public async Task LoginAsync_TooManyFailedAttempts_LocksAccount()
    {
        await CreateUserAsync("ahmed", "correct-password");

        // Default policy: 5 failed attempts locks the account.
        for (var i = 0; i < 5; i++)
        {
            await AuthenticationService.LoginAsync("ahmed", "wrong", rememberMe: false, CancellationToken.None);
        }

        var result = await AuthenticationService.LoginAsync("ahmed", "correct-password", rememberMe: false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("AUTH_ACCOUNT_LOCKED", result.Error!.Code);
    }

    [Fact]
    public async Task LoginAsync_SuccessAfterFailures_ResetsFailedCount()
    {
        await CreateUserAsync("ahmed", "correct-password");

        await AuthenticationService.LoginAsync("ahmed", "wrong", rememberMe: false, CancellationToken.None);
        await AuthenticationService.LoginAsync("ahmed", "wrong", rememberMe: false, CancellationToken.None);

        var success = await AuthenticationService.LoginAsync("ahmed", "correct-password", rememberMe: false, CancellationToken.None);
        Assert.True(success.IsSuccess);

        // Now 4 more failures (would have been 6 total without the reset, well past the lockout
        // threshold) should NOT lock, proving the counter actually reset on success.
        for (var i = 0; i < 4; i++)
        {
            await AuthenticationService.LoginAsync("ahmed", "wrong", rememberMe: false, CancellationToken.None);
        }

        var stillUnlocked = await AuthenticationService.LoginAsync("ahmed", "correct-password", rememberMe: false, CancellationToken.None);
        Assert.True(stillUnlocked.IsSuccess);
    }

    [Fact]
    public async Task LoginAsync_DeactivatedUser_Fails()
    {
        var uid = await CreateUserAsync("ahmed", "correct-password");
        await UserManagementService.DeactivateUserAsync(uid, CancellationToken.None);
        await CreateUserAsync("second-admin-to-avoid-last-admin-guard", "x", Core.Permissions.UserRole.Admin);

        var result = await AuthenticationService.LoginAsync("ahmed", "correct-password", rememberMe: false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("AUTH_INVALID_CREDENTIALS", result.Error!.Code);
    }

    [Fact]
    public async Task ValidateSessionAsync_ValidToken_ReturnsPrincipal()
    {
        await CreateUserAsync("ahmed", "correct-password");
        var login = await AuthenticationService.LoginAsync("ahmed", "correct-password", rememberMe: false, CancellationToken.None);

        var principal = await AuthenticationService.ValidateSessionAsync(login.Value.SessionToken, CancellationToken.None);

        Assert.NotNull(principal);
        Assert.Equal("ahmed", principal!.DisplayName);
    }

    [Fact]
    public async Task ValidateSessionAsync_UnknownToken_ReturnsNull()
    {
        var principal = await AuthenticationService.ValidateSessionAsync("not-a-real-token", CancellationToken.None);

        Assert.Null(principal);
    }

    [Fact]
    public async Task LogoutAsync_ThenValidate_ReturnsNull()
    {
        await CreateUserAsync("ahmed", "correct-password");
        var login = await AuthenticationService.LoginAsync("ahmed", "correct-password", rememberMe: false, CancellationToken.None);

        await AuthenticationService.LogoutAsync(login.Value.SessionToken, CancellationToken.None);
        var principal = await AuthenticationService.ValidateSessionAsync(login.Value.SessionToken, CancellationToken.None);

        Assert.Null(principal);
    }

    [Fact]
    public async Task LoginAsync_RememberMe_ProducesLongerExpiry()
    {
        await CreateUserAsync("short", "password");
        await CreateUserAsync("remembered", "password");

        var shortLived = await AuthenticationService.LoginAsync("short", "password", rememberMe: false, CancellationToken.None);
        var remembered = await AuthenticationService.LoginAsync("remembered", "password", rememberMe: true, CancellationToken.None);

        Assert.True(remembered.Value.ExpiresAt > shortLived.Value.ExpiresAt);
    }
}
