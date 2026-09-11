using Darhous.Archive.Core.Permissions;
using Darhous.Archive.Security.Permissions;

namespace Darhous.Archive.Security.Tests;

public class DefaultPermissionEvaluatorTests
{
    private readonly DefaultPermissionEvaluator _evaluator = new();

    [Fact]
    public void Admin_HasSettingsWrite()
    {
        Assert.True(_evaluator.HasPermission(UserRole.Admin, WellKnownPermissions.SettingsWrite));
    }

    [Fact]
    public void User_DoesNotHaveSettingsWrite()
    {
        Assert.False(_evaluator.HasPermission(UserRole.User, WellKnownPermissions.SettingsWrite));
    }

    [Fact]
    public void User_CanWriteDocuments()
    {
        Assert.True(_evaluator.HasPermission(UserRole.User, WellKnownPermissions.DocumentsWrite));
    }

    [Fact]
    public void ReadOnly_CannotWriteDocuments()
    {
        Assert.False(_evaluator.HasPermission(UserRole.ReadOnly, WellKnownPermissions.DocumentsWrite));
    }

    [Fact]
    public void ReadOnly_CanReadDocuments()
    {
        Assert.True(_evaluator.HasPermission(UserRole.ReadOnly, WellKnownPermissions.DocumentsRead));
    }

    [Fact]
    public void Guest_MatchesReadOnlyCapabilities()
    {
        // SAD §13 Guest Mode: "صلاحياته: Read Only فقط".
        Assert.Equal(
            _evaluator.HasPermission(UserRole.ReadOnly, WellKnownPermissions.DocumentsRead),
            _evaluator.HasPermission(UserRole.Guest, WellKnownPermissions.DocumentsRead));
        Assert.False(_evaluator.HasPermission(UserRole.Guest, WellKnownPermissions.DocumentsWrite));
        Assert.False(_evaluator.HasPermission(UserRole.Guest, WellKnownPermissions.SettingsRead));
    }
}
