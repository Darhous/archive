using System.Data;
using Dapper;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Core.Permissions;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;

namespace Darhous.Archive.Persistence.Repositories;

/// <summary>Same dual-mode pattern as <see cref="RoleRepository"/> — see its doc comment.</summary>
public sealed class AppUserRepository : IAppUserRepository
{
    private const string SelectColumns =
        """
        SELECT u.uid, u.username, u.display_name, u.password_hash, u.password_scheme,
               r.code AS role_code, u.is_active, u.must_change_password, u.failed_login_count,
               u.locked_until, u.last_login_at, u.created_at, u.updated_at
        FROM app_users u
        JOIN roles r ON r.id = u.role_id
        """;

    private readonly ISqliteConnectionFactory? _connectionFactory;
    private readonly IDbConnection? _boundConnection;
    private readonly IDbTransaction? _boundTransaction;

    public AppUserRepository(ISqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public AppUserRepository(IDbConnection connection, IDbTransaction transaction)
    {
        _boundConnection = connection;
        _boundTransaction = transaction;
    }

    public async Task<Guid> CreateAsync(
        string username, string displayName, string passwordHash, string passwordScheme,
        UserRole role, bool mustChangePassword, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(CreateAsync));

        var uid = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var command = new CommandDefinition(
            """
            INSERT INTO app_users
                (uid, username, display_name, password_hash, password_scheme, role_id,
                 is_active, must_change_password, failed_login_count, created_at, updated_at)
            VALUES
                (@Uid, @Username, @DisplayName, @PasswordHash, @PasswordScheme,
                 (SELECT id FROM roles WHERE code = @RoleCode),
                 1, @MustChangePassword, 0, @Now, @Now);
            """,
            new
            {
                Uid = uid.ToString(),
                Username = username,
                DisplayName = displayName,
                PasswordHash = passwordHash,
                PasswordScheme = passwordScheme,
                RoleCode = RoleCodeMapping.ToCode(role),
                MustChangePassword = mustChangePassword ? 1 : 0,
                Now = now,
            },
            _boundTransaction,
            cancellationToken: cancellationToken);

        await _boundConnection!.ExecuteAsync(command);
        return uid;
    }

    public Task<AppUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
        QuerySingleAsync($"{SelectColumns} WHERE u.username = @Username;", new { Username = username }, cancellationToken);

    public Task<AppUser?> GetByUidAsync(Guid uid, CancellationToken cancellationToken) =>
        QuerySingleAsync($"{SelectColumns} WHERE u.uid = @Uid;", new { Uid = uid.ToString() }, cancellationToken);

    public async Task<IReadOnlyList<AppUser>> ListAsync(CancellationToken cancellationToken)
    {
        var sql = $"{SelectColumns} ORDER BY u.id;";
        var rows = await QueryAsync<AppUserRow>(sql, null, cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<int> CountActiveByRoleAsync(UserRole role, CancellationToken cancellationToken)
    {
        const string sql =
            """
            SELECT COUNT(*) FROM app_users u
            JOIN roles r ON r.id = u.role_id
            WHERE r.code = @RoleCode AND u.is_active = 1;
            """;
        var parameters = new { RoleCode = RoleCodeMapping.ToCode(role) };

        if (_boundConnection is not null)
        {
            return await _boundConnection.ExecuteScalarAsync<int>(
                new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public Task RecordLoginSuccessAsync(Guid uid, DateTimeOffset loginAt, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(RecordLoginSuccessAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            """
            UPDATE app_users
            SET failed_login_count = 0, locked_until = NULL, last_login_at = @LoginAt, updated_at = @LoginAt
            WHERE uid = @Uid;
            """,
            new { Uid = uid.ToString(), LoginAt = loginAt.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task RecordLoginFailureAsync(Guid uid, DateTimeOffset? lockedUntil, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(RecordLoginFailureAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            """
            UPDATE app_users
            SET failed_login_count = failed_login_count + 1,
                locked_until = COALESCE(@LockedUntil, locked_until),
                updated_at = @Now
            WHERE uid = @Uid;
            """,
            new
            {
                Uid = uid.ToString(),
                LockedUntil = lockedUntil?.ToUnixTimeMilliseconds(),
                Now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task SetActiveAsync(Guid uid, bool isActive, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(SetActiveAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "UPDATE app_users SET is_active = @IsActive, updated_at = @Now WHERE uid = @Uid;",
            new { Uid = uid.ToString(), IsActive = isActive ? 1 : 0, Now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    public Task ChangeRoleAsync(Guid uid, UserRole role, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(ChangeRoleAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            """
            UPDATE app_users
            SET role_id = (SELECT id FROM roles WHERE code = @RoleCode), updated_at = @Now
            WHERE uid = @Uid;
            """,
            new { Uid = uid.ToString(), RoleCode = RoleCodeMapping.ToCode(role), Now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    private void RequireWriteMode(string memberName)
    {
        if (_boundConnection is null)
        {
            throw new InvalidOperationException(
                $"{nameof(AppUserRepository)}.{memberName} must run inside IUnitOfWork.ExecuteAsync.");
        }
    }

    private async Task<AppUser?> QuerySingleAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        if (_boundConnection is not null)
        {
            var row = await _boundConnection.QuerySingleOrDefaultAsync<AppUserRow>(
                new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
            return row is null ? null : Map(row);
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRow = await connection.QuerySingleOrDefaultAsync<AppUserRow>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return readRow is null ? null : Map(readRow);
    }

    private async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters, CancellationToken cancellationToken)
    {
        if (_boundConnection is not null)
        {
            return await _boundConnection.QueryAsync<T>(
                new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        return await connection.QueryAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    private static AppUser Map(AppUserRow row) => new(
        Guid.Parse(row.Uid), row.Username, row.DisplayName, row.PasswordHash, row.PasswordScheme,
        RoleCodeMapping.FromCode(row.RoleCode), row.IsActive == 1, row.MustChangePassword == 1,
        (int)row.FailedLoginCount,
        row.LockedUntil.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(row.LockedUntil.Value) : null,
        row.LastLoginAt.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(row.LastLoginAt.Value) : null,
        DateTimeOffset.FromUnixTimeMilliseconds(row.CreatedAt),
        DateTimeOffset.FromUnixTimeMilliseconds(row.UpdatedAt));

    private sealed class AppUserRow
    {
        public string Uid { get; set; } = "";
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string PasswordScheme { get; set; } = "";
        public string RoleCode { get; set; } = "";
        public long IsActive { get; set; }
        public long MustChangePassword { get; set; }
        public long FailedLoginCount { get; set; }
        public long? LockedUntil { get; set; }
        public long? LastLoginAt { get; set; }
        public long CreatedAt { get; set; }
        public long UpdatedAt { get; set; }
    }
}
