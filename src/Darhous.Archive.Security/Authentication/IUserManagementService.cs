using Darhous.Archive.Core.Permissions;
using Darhous.Archive.Core.Results;

namespace Darhous.Archive.Security.Authentication;

/// <summary>Admin-only user management (SAD §13 Admin capabilities, §179 Role Change, §180 Last Admin Protection).</summary>
public interface IUserManagementService
{
    Task<Result<Guid>> CreateUserAsync(
        string username, string displayName, string initialPassword, UserRole role,
        bool mustChangePassword, CancellationToken cancellationToken);

    /// <summary>Fails with AUTH_LAST_ADMIN if <paramref name="targetUid"/> is the last active Admin.</summary>
    Task<Result> DeactivateUserAsync(Guid targetUid, CancellationToken cancellationToken);

    /// <summary>Fails with AUTH_LAST_ADMIN if this would leave zero active Admins.</summary>
    Task<Result> ChangeRoleAsync(Guid targetUid, UserRole newRole, CancellationToken cancellationToken);
}
