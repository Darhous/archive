namespace Darhous.Archive.Application.Persistence;

public interface ISessionRepository
{
    Task<Guid> CreateAsync(
        Guid userUid, string tokenHash, bool rememberMe, DateTimeOffset expiresAt,
        CancellationToken cancellationToken);

    Task<Session?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task TouchAsync(Guid sessionUid, DateTimeOffset seenAt, CancellationToken cancellationToken);

    Task RevokeAsync(Guid sessionUid, DateTimeOffset revokedAt, CancellationToken cancellationToken);

    Task RevokeAllForUserAsync(Guid userUid, DateTimeOffset revokedAt, CancellationToken cancellationToken);
}
