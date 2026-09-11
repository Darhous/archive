namespace Darhous.Archive.Core.Permissions;

/// <summary>
/// SAD §13 — Admin / User / ReadOnly / Guest. Ordered from least to most privileged so
/// callers can compare roles with <c>&lt;</c>/<c>&gt;</c> when a simple tier check suffices.
/// </summary>
public enum UserRole
{
    Guest,
    ReadOnly,
    User,
    Admin,
}
