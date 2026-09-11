namespace Darhous.Archive.Application.Persistence;

/// <summary>DB Spec §21 (user_sessions) — public shape. Never carries the raw token or its hash.</summary>
public sealed record Session(
    Guid Uid,
    Guid UserUid,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset LastSeenAt,
    bool RememberMe,
    DateTimeOffset? RevokedAt)
{
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
