using System.Data;
using Dapper;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;

namespace Darhous.Archive.Persistence.Repositories;

/// <summary>
/// The one repository built in Phase 2 to prove the pattern end to end. Two construction
/// modes, both backed by the same query/mapping logic:
/// - Read-only (<see cref="RoleRepository(ISqliteConnectionFactory)"/>): registered directly
///   in DI, opens a short-lived read connection per call — reads never go through the write
///   queue (WAL allows concurrent readers).
/// - Transactional (<see cref="RoleRepository(IDbConnection, IDbTransaction)"/>): created by
///   <c>SqliteUnitOfWorkContext</c> for the duration of one <see cref="IUnitOfWork.ExecuteAsync{TResult}"/>
///   callback; <see cref="CreateAsync"/> only works in this mode, since a write must be part
///   of the enclosing transaction.
/// </summary>
public sealed class RoleRepository : IRoleRepository
{
    private readonly ISqliteConnectionFactory? _connectionFactory;
    private readonly IDbConnection? _boundConnection;
    private readonly IDbTransaction? _boundTransaction;

    public RoleRepository(ISqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public RoleRepository(IDbConnection connection, IDbTransaction transaction)
    {
        _boundConnection = connection;
        _boundTransaction = transaction;
    }

    public async Task<Guid> CreateAsync(string code, string displayName, bool isSystem, CancellationToken cancellationToken)
    {
        if (_boundConnection is null)
        {
            throw new InvalidOperationException(
                $"{nameof(RoleRepository)}.{nameof(CreateAsync)} must run inside {nameof(IUnitOfWork)}.{nameof(IUnitOfWork.ExecuteAsync)}.");
        }

        var uid = Guid.CreateVersion7();

        var command = new CommandDefinition(
            """
            INSERT INTO roles (uid, code, display_name, is_system, created_at)
            VALUES (@Uid, @Code, @DisplayName, @IsSystem, @CreatedAt);
            """,
            new
            {
                Uid = uid.ToString(),
                Code = code,
                DisplayName = displayName,
                IsSystem = isSystem ? 1 : 0,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            },
            _boundTransaction,
            cancellationToken: cancellationToken);

        await _boundConnection.ExecuteAsync(command);
        return uid;
    }

    public Task<Role?> GetByUidAsync(Guid uid, CancellationToken cancellationToken) =>
        QuerySingleAsync("WHERE uid = @Uid", new { Uid = uid.ToString() }, cancellationToken);

    public Task<Role?> GetByCodeAsync(string code, CancellationToken cancellationToken) =>
        QuerySingleAsync("WHERE code = @Code", new { Code = code }, cancellationToken);

    public async Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT uid, code, display_name, is_system, created_at FROM roles ORDER BY id;";

        if (_boundConnection is not null)
        {
            var rows = await _boundConnection.QueryAsync<RoleRow>(
                new CommandDefinition(sql, transaction: _boundTransaction, cancellationToken: cancellationToken));
            return rows.Select(Map).ToList();
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRows = await connection.QueryAsync<RoleRow>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return readRows.Select(Map).ToList();
    }

    private async Task<Role?> QuerySingleAsync(string whereClause, object parameters, CancellationToken cancellationToken)
    {
        var sql = $"SELECT uid, code, display_name, is_system, created_at FROM roles {whereClause};";

        if (_boundConnection is not null)
        {
            var row = await _boundConnection.QuerySingleOrDefaultAsync<RoleRow>(
                new CommandDefinition(sql, parameters, _boundTransaction, cancellationToken: cancellationToken));
            return row is null ? null : Map(row);
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRow = await connection.QuerySingleOrDefaultAsync<RoleRow>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return readRow is null ? null : Map(readRow);
    }

    private static Role Map(RoleRow row) => new(
        Guid.Parse(row.Uid), row.Code, row.DisplayName, row.IsSystem == 1,
        DateTimeOffset.FromUnixTimeMilliseconds(row.CreatedAt));

    // Dapper needs a settable-property shape matching the snake_case columns; the public
    // Role record stays immutable/PascalCase for callers.
    private sealed class RoleRow
    {
        public string Uid { get; set; } = "";
        public string Code { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public long IsSystem { get; set; }
        public long CreatedAt { get; set; }
    }
}
