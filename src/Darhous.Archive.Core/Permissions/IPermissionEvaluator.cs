namespace Darhous.Archive.Core.Permissions;

/// <summary>
/// Decides whether a role/permission combination is allowed. The concrete role→permission
/// matrix (SAD §13: what Admin/User/ReadOnly/Guest can each do) is wired up in Phase 3
/// (Authentication &amp; Roles) — this interface only fixes the shape so other Phase 1
/// abstractions (Modules, Jobs) can depend on it without waiting.
/// </summary>
public interface IPermissionEvaluator
{
    bool HasPermission(UserRole role, Permission permission);
}
