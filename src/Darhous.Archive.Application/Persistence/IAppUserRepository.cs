using Darhous.Archive.Core.Permissions;

namespace Darhous.Archive.Application.Persistence;

/// <summary>Everything the Authentication flow (Phase 3) needs against `app_users`.</summary>
public interface IAppUserRepository
{
    Task<Guid> CreateAsync(
        string username, string displayName, string passwordHash, string passwordScheme,
        UserRole role, bool mustChangePassword, CancellationToken cancellationToken);

    Task<AppUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken);

    Task<AppUser?> GetByUidAsync(Guid uid, CancellationToken cancellationToken);

    Task<IReadOnlyList<AppUser>> ListAsync(CancellationToken cancellationToken);

    /// <summary>SAD §180 Last Admin Protection — counts active (is_active=1) users in the given role.</summary>
    Task<int> CountActiveByRoleAsync(UserRole role, CancellationToken cancellationToken);

    Task RecordLoginSuccessAsync(Guid uid, DateTimeOffset loginAt, CancellationToken cancellationToken);

    /// <summary>Increments failed_login_count and, when the threshold is crossed, sets locked_until.</summary>
    Task RecordLoginFailureAsync(Guid uid, DateTimeOffset? lockedUntil, CancellationToken cancellationToken);

    Task SetActiveAsync(Guid uid, bool isActive, CancellationToken cancellationToken);

    Task ChangeRoleAsync(Guid uid, UserRole role, CancellationToken cancellationToken);
}
