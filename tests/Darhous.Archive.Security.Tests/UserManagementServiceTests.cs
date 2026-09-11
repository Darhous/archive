using Darhous.Archive.Core.Permissions;

namespace Darhous.Archive.Security.Tests;

public class UserManagementServiceTests : AuthenticationTestBase
{
    [Fact]
    public async Task CreateUserAsync_DuplicateUsername_Fails()
    {
        await CreateUserAsync("ahmed", "password");

        var result = await UserManagementService.CreateUserAsync(
            "ahmed", "Someone Else", "another-password", UserRole.User, false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("AUTH_USERNAME_TAKEN", result.Error!.Code);
    }

    [Fact]
    public async Task CreateUserAsync_NewUsername_Succeeds_AndPasswordIsHashedNotPlaintext()
    {
        var result = await UserManagementService.CreateUserAsync(
            "newuser", "New User", "s3cr3t", UserRole.User, false, CancellationToken.None);

        Assert.True(result.IsSuccess);

        var login = await AuthenticationService.LoginAsync("newuser", "s3cr3t", false, CancellationToken.None);
        Assert.True(login.IsSuccess);
    }

    [Fact]
    public async Task DeactivateUserAsync_LastActiveAdmin_IsBlocked()
    {
        var adminUid = await CreateUserAsync("only-admin", "password", UserRole.Admin);

        var result = await UserManagementService.DeactivateUserAsync(adminUid, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("AUTH_LAST_ADMIN", result.Error!.Code);
    }

    [Fact]
    public async Task DeactivateUserAsync_OneOfTwoAdmins_Succeeds()
    {
        var admin1 = await CreateUserAsync("admin-one", "password", UserRole.Admin);
        await CreateUserAsync("admin-two", "password", UserRole.Admin);

        var result = await UserManagementService.DeactivateUserAsync(admin1, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DeactivateUserAsync_RevokesActiveSessions()
    {
        var uid = await CreateUserAsync("ahmed", "password");
        await CreateUserAsync("keeper-admin", "password", UserRole.Admin);
        var login = await AuthenticationService.LoginAsync("ahmed", "password", false, CancellationToken.None);

        await UserManagementService.DeactivateUserAsync(uid, CancellationToken.None);

        var principal = await AuthenticationService.ValidateSessionAsync(login.Value.SessionToken, CancellationToken.None);
        Assert.Null(principal);
    }

    [Fact]
    public async Task ChangeRoleAsync_AwayFromLastAdmin_IsBlocked()
    {
        var adminUid = await CreateUserAsync("only-admin", "password", UserRole.Admin);

        var result = await UserManagementService.ChangeRoleAsync(adminUid, UserRole.User, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("AUTH_LAST_ADMIN", result.Error!.Code);
    }

    [Fact]
    public async Task ChangeRoleAsync_WithAnotherAdminPresent_Succeeds()
    {
        var admin1 = await CreateUserAsync("admin-one", "password", UserRole.Admin);
        await CreateUserAsync("admin-two", "password", UserRole.Admin);

        var result = await UserManagementService.ChangeRoleAsync(admin1, UserRole.User, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ChangeRoleAsync_UnknownUser_Fails()
    {
        var result = await UserManagementService.ChangeRoleAsync(Guid.NewGuid(), UserRole.User, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("AUTH_USER_NOT_FOUND", result.Error!.Code);
    }
}
