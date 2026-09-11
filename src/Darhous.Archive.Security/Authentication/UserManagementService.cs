using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Core.Permissions;
using Darhous.Archive.Core.Results;
using Darhous.Archive.Security.Passwords;

namespace Darhous.Archive.Security.Authentication;

public sealed class UserManagementService(IUnitOfWork unitOfWork, IPasswordHasher passwordHasher) : IUserManagementService
{
    public Task<Result<Guid>> CreateUserAsync(
        string username, string displayName, string initialPassword, UserRole role,
        bool mustChangePassword, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async (context, ct) =>
        {
            var existing = await context.Users.GetByUsernameAsync(username, ct);
            if (existing is not null)
            {
                return Result<Guid>.Failure(Error.Of("AUTH_USERNAME_TAKEN", "اسم المستخدم مستخدم بالفعل."));
            }

            var hash = passwordHasher.Hash(initialPassword);
            var uid = await context.Users.CreateAsync(username, displayName, hash, "argon2id", role, mustChangePassword, ct);
            return Result<Guid>.Success(uid);
        }, cancellationToken);

    public Task<Result> DeactivateUserAsync(Guid targetUid, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async (context, ct) =>
        {
            var user = await context.Users.GetByUidAsync(targetUid, ct);
            if (user is null)
            {
                return Result.Failure(Error.Of("AUTH_USER_NOT_FOUND", "المستخدم غير موجود."));
            }

            if (await IsLastActiveAdminAsync(context, user, ct))
            {
                return Result.Failure(Error.Of("AUTH_LAST_ADMIN", "لا يمكن تعطيل آخر Admin نشط (SAD §180)."));
            }

            await context.Users.SetActiveAsync(targetUid, isActive: false, ct);
            await context.Sessions.RevokeAllForUserAsync(targetUid, DateTimeOffset.UtcNow, ct);
            return Result.Success();
        }, cancellationToken);

    public Task<Result> ChangeRoleAsync(Guid targetUid, UserRole newRole, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async (context, ct) =>
        {
            var user = await context.Users.GetByUidAsync(targetUid, ct);
            if (user is null)
            {
                return Result.Failure(Error.Of("AUTH_USER_NOT_FOUND", "المستخدم غير موجود."));
            }

            if (newRole != UserRole.Admin && await IsLastActiveAdminAsync(context, user, ct))
            {
                return Result.Failure(Error.Of("AUTH_LAST_ADMIN", "لا يمكن تحويل آخر Admin نشط إلى دور آخر (SAD §180)."));
            }

            await context.Users.ChangeRoleAsync(targetUid, newRole, ct);
            return Result.Success();
        }, cancellationToken);

    private static async Task<bool> IsLastActiveAdminAsync(IUnitOfWorkContext context, AppUser user, CancellationToken cancellationToken)
    {
        if (user.Role != UserRole.Admin || !user.IsActive)
        {
            return false;
        }

        var activeAdmins = await context.Users.CountActiveByRoleAsync(UserRole.Admin, cancellationToken);
        return activeAdmins <= 1;
    }
}
