namespace Darhous.Archive.Security.Authentication;

/// <summary>
/// SAD §14 (نظام الدخول) leaves exact lockout/session-lifetime thresholds unspecified —
/// these defaults were picked during Phase 3 implementation, same pattern as the Argon2id
/// parameters in Phase 1 (DB Spec app_users section): reasonable values now, adjustable via
/// app_settings later without a code change.
/// </summary>
public sealed record AuthenticationOptions(
    int MaxFailedAttempts = 5,
    int LockoutMinutes = 15,
    int SessionHours = 8,
    int RememberMeDays = 30);
