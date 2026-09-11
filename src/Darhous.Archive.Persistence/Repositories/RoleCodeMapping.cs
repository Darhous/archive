using Darhous.Archive.Core.Permissions;

namespace Darhous.Archive.Persistence.Repositories;

/// <summary>Maps Core's <see cref="UserRole"/> enum to the `roles.code` seeded by M202609110003_SeedRoles.</summary>
internal static class RoleCodeMapping
{
    public static string ToCode(UserRole role) => role switch
    {
        UserRole.Admin => "admin",
        UserRole.User => "user",
        UserRole.ReadOnly => "readonly",
        UserRole.Guest => "guest",
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    public static UserRole FromCode(string code) => code switch
    {
        "admin" => UserRole.Admin,
        "user" => UserRole.User,
        "readonly" => UserRole.ReadOnly,
        "guest" => UserRole.Guest,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown role code."),
    };
}
