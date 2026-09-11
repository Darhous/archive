using Darhous.Archive.Core.Permissions;

namespace Darhous.Archive.Security.Permissions;

/// <summary>
/// The role→permission matrix Phase 1 deferred (see Core's IPermissionEvaluator doc comment),
/// built from SAD §13's per-role capability lists. Settings are split coarsely here
/// (settings.read/write) — the finer "which specific settings are sensitive" distinction SAD
/// §13 alludes to ("Settings الحساسة") doesn't have anywhere to attach to yet (the Settings UI
/// itself is a later phase); this matrix will need revisiting once that exists.
/// </summary>
public sealed class DefaultPermissionEvaluator : IPermissionEvaluator
{
    private static readonly IReadOnlyDictionary<UserRole, HashSet<Permission>> Matrix = new Dictionary<UserRole, HashSet<Permission>>
    {
        [UserRole.Admin] =
        [
            WellKnownPermissions.DocumentsRead, WellKnownPermissions.DocumentsWrite, WellKnownPermissions.DocumentsDelete,
            WellKnownPermissions.MetadataRead, WellKnownPermissions.MetadataWrite,
            WellKnownPermissions.NetworkOutbound,
            WellKnownPermissions.DeviceScan, WellKnownPermissions.DevicePrint,
            WellKnownPermissions.SettingsRead, WellKnownPermissions.SettingsWrite,
            WellKnownPermissions.NotificationsSend,
        ],
        [UserRole.User] =
        [
            WellKnownPermissions.DocumentsRead, WellKnownPermissions.DocumentsWrite, WellKnownPermissions.DocumentsDelete,
            WellKnownPermissions.MetadataRead, WellKnownPermissions.MetadataWrite,
            WellKnownPermissions.DeviceScan, WellKnownPermissions.DevicePrint,
            WellKnownPermissions.SettingsRead,
            WellKnownPermissions.NotificationsSend,
        ],
        [UserRole.ReadOnly] =
        [
            WellKnownPermissions.DocumentsRead, WellKnownPermissions.MetadataRead,
        ],
        // SAD §13 Guest Mode: "صلاحياته: Read Only فقط" — identical to ReadOnly.
        [UserRole.Guest] =
        [
            WellKnownPermissions.DocumentsRead, WellKnownPermissions.MetadataRead,
        ],
    };

    public bool HasPermission(UserRole role, Permission permission) =>
        Matrix.TryGetValue(role, out var permissions) && permissions.Contains(permission);
}
