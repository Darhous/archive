using System.Data;
using Dapper;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;

namespace Darhous.Archive.Persistence.Repositories;

/// <summary>Same dual-mode pattern as <see cref="RoleRepository"/> — see its doc comment.</summary>
public sealed class SessionRepository : ISessionRepository
{
    private const string SelectColumns =
        """
        SELECT s.uid, u.uid AS user_uid, s.created_at, s.expires_at, s.last_seen_at, s.remember_me, s.revoked_at
        FROM user_sessions s
        JOIN app_users u ON u.id = s.user_id
        """;

    private readonly ISqliteConnectionFactory? _connectionFactory;
    private readonly IDbConnection? _boundConnection;
    private readonly IDbTransaction? _boundTransaction;

    public SessionRepository(ISqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public SessionRepository(IDbConnection connection, IDbTransaction transaction)
    {
        _boundConnection = connection;
        _boundTransaction = transaction;
    }

    public async Task<Guid> CreateAsync(
        Guid userUid, string tokenHash, bool rememberMe, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(CreateAsync));

        var uid = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await _boundConnection!.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO user_sessions (uid, user_id, token_hash, created_at, expires_at, last_seen_at, remember_me)
            VALUES (@Uid, (SELECT id FROM app_users WHERE uid = @UserUid), @TokenHash, @Now, @ExpiresAt, @Now, @RememberMe);
            """,
            new
            {
                Uid = uid.ToString(),
                UserUid = userUid.ToString(),
                TokenHash = tokenHash,
                Now = now,
                ExpiresAt = expiresAt.ToUnixTimeMilliseconds(),
                RememberMe = rememberMe ? 1 : 0,
            },
            _boundTransaction,
            cancellationToken: cancellationToken));

        return uid;
    }

    public async Task<Session?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var sql = $"{SelectColumns} WHERE s.token_hash = @TokenHash;";
        var parameters = new { TokenHash = tokenHash };

        if (_boundConnection is not null)
        {
            var row = await _boundConnection.QuerySingleOrDefaultAsync<SessionRow>(
                new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
            return row is null ? null : Map(row);
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRow = await connection.QuerySingleOrDefaultAsync<SessionRow>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return readRow is null ? null : Map(readRow);
    }

    public Task TouchAsync(Guid sessionUid, DateTimeOffset seenAt, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(TouchAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "UPDATE user_sessions SET last_seen_at = @SeenAt WHERE uid = @Uid;",
            new { Uid = sessionUid.ToString(), SeenAt = seenAt.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task RevokeAsync(Guid sessionUid, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(RevokeAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "UPDATE user_sessions SET revoked_at = @RevokedAt WHERE uid = @Uid;",
            new { Uid = sessionUid.ToString(), RevokedAt = revokedAt.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task RevokeAllForUserAsync(Guid userUid, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(RevokeAllForUserAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            """
            UPDATE user_sessions
            SET revoked_at = @RevokedAt
            WHERE revoked_at IS NULL AND user_id = (SELECT id FROM app_users WHERE uid = @UserUid);
            """,
            new { UserUid = userUid.ToString(), RevokedAt = revokedAt.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    private void RequireWriteMode(string memberName)
    {
        if (_boundConnection is null)
        {
            throw new InvalidOperationException(
                $"{nameof(SessionRepository)}.{memberName} must run inside IUnitOfWork.ExecuteAsync.");
        }
    }

    private static Session Map(SessionRow row) => new(
        Guid.Parse(row.Uid), Guid.Parse(row.UserUid),
        DateTimeOffset.FromUnixTimeMilliseconds(row.CreatedAt),
        DateTimeOffset.FromUnixTimeMilliseconds(row.ExpiresAt),
        DateTimeOffset.FromUnixTimeMilliseconds(row.LastSeenAt),
        row.RememberMe == 1,
        row.RevokedAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(row.RevokedAt.Value) : null);

    private sealed class SessionRow
    {
        public string Uid { get; set; } = "";
        public string UserUid { get; set; } = "";
        public long CreatedAt { get; set; }
        public long ExpiresAt { get; set; }
        public long LastSeenAt { get; set; }
        public long RememberMe { get; set; }
        public long? RevokedAt { get; set; }
    }
}
