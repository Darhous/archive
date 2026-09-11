using Darhous.Archive.Core.Permissions;

namespace Darhous.Archive.Application.Persistence;

/// <summary>DB Spec (app_users table) — public shape; the SQLite rowid never leaves Persistence.</summary>
public sealed record AppUser(
    Guid Uid,
    string Username,
    string DisplayName,
    string PasswordHash,
    string PasswordScheme,
    UserRole Role,
    bool IsActive,
    bool MustChangePassword,
    int FailedLoginCount,
    DateTimeOffset? LockedUntil,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
