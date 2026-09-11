using Darhous.Archive.Core.Permissions;

namespace Darhous.Archive.Security.Sessions;

/// <summary>
/// The authenticated (or Guest) identity for the current session (SAD §13-14). Full session
/// persistence (`user_sessions` table, Remember Me, timeout) is built in Phase 3 — this
/// record only fixes the shape so Phase 1 code (permission checks, audit) can depend on it.
/// </summary>
public sealed record ArchivePrincipal(Guid? UserId, string DisplayName, UserRole Role)
{
    public bool IsGuest => UserId is null;

    /// <summary>SAD §13 Guest Mode — Read Only, present only when Guest Mode is enabled in Settings.</summary>
    public static ArchivePrincipal Guest { get; } = new(UserId: null, DisplayName: "Guest", UserRole.Guest);
}
