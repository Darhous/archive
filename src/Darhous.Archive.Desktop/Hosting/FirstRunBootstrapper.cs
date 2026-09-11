using System.Security.Cryptography;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Core.Permissions;
using Darhous.Archive.Security.Authentication;

namespace Darhous.Archive.Desktop.Hosting;

/// <summary>
/// The DB Spec seeds the 4 roles via migration, but `app_users` starts empty — nobody could
/// ever log in otherwise. On first run (zero active Admins found), creates one with a
/// randomly generated password shown to the operator exactly once and never persisted in
/// plaintext (DB Spec §115 Security Rules: "No plaintext passwords"), with
/// must_change_password=true so it cannot silently become a permanent shared secret.
/// </summary>
public static class FirstRunBootstrapper
{
    public static async Task<string?> EnsureAdminExistsAsync(
        IUnitOfWork unitOfWork, IUserManagementService userManagementService, CancellationToken cancellationToken)
    {
        var hasAdmin = await unitOfWork.ExecuteAsync(
            (context, ct) => context.Users.CountActiveByRoleAsync(UserRole.Admin, ct), cancellationToken);

        if (hasAdmin > 0)
        {
            return null;
        }

        var generatedPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
        var result = await userManagementService.CreateUserAsync(
            "admin", "Administrator", generatedPassword, UserRole.Admin, mustChangePassword: true, cancellationToken);

        return result.IsSuccess ? generatedPassword : null;
    }
}
