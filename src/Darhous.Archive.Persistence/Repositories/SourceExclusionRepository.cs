using System.Data;
using Dapper;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;

namespace Darhous.Archive.Persistence.Repositories;

public sealed class SourceExclusionRepository : ISourceExclusionRepository
{
    private const string SelectColumns =
        """
        SELECT e.uid, e.exclusion_type, e.path, e.path_normalized, e.is_system, e.is_enabled,
               e.reason, u.uid AS created_by, e.created_at
        FROM source_exclusions e
        LEFT JOIN app_users u ON u.id = e.created_by
        """;

    private readonly ISqliteConnectionFactory? _connectionFactory;
    private readonly IDbConnection? _boundConnection;
    private readonly IDbTransaction? _boundTransaction;

    public SourceExclusionRepository(ISqliteConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public SourceExclusionRepository(IDbConnection connection, IDbTransaction transaction)
    {
        _boundConnection = connection;
        _boundTransaction = transaction;
    }

    public async Task<Guid> CreateIfMissingAsync(NewSourceExclusion exclusion, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(CreateIfMissingAsync));

        var normalized = NormalizePath(exclusion.Path);
        var existing = await _boundConnection!.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT uid FROM source_exclusions WHERE path_normalized = @Normalized;",
            new { Normalized = normalized }, _boundTransaction, cancellationToken: cancellationToken));

        if (existing is not null)
        {
            return Guid.Parse(existing);
        }

        var uid = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await _boundConnection!.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO source_exclusions (uid, exclusion_type, path, path_normalized, is_system, is_enabled, reason, created_by, created_at, updated_at)
            VALUES (@Uid, @ExclusionType, @Path, @PathNormalized, @IsSystem, 1, @Reason,
                    (SELECT id FROM app_users WHERE uid = @CreatedBy), @Now, @Now);
            """,
            new
            {
                Uid = uid.ToString(), exclusion.ExclusionType, exclusion.Path, PathNormalized = normalized,
                IsSystem = exclusion.IsSystem ? 1 : 0, exclusion.Reason, CreatedBy = exclusion.CreatedBy?.ToString(), Now = now,
            },
            _boundTransaction,
            cancellationToken: cancellationToken));

        return uid;
    }

    public async Task<IReadOnlyList<SourceExclusion>> ListEnabledAsync(CancellationToken cancellationToken)
    {
        var sql = $"{SelectColumns} WHERE e.is_enabled = 1 ORDER BY e.created_at;";

        if (_boundConnection is not null)
        {
            var rows = await _boundConnection.QueryAsync<ExclusionRow>(
                new CommandDefinition(sql, transaction: _boundTransaction, cancellationToken: cancellationToken));
            return rows.Select(Map).ToList();
        }

        await using var connection = await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var readRows = await connection.QueryAsync<ExclusionRow>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return readRows.Select(Map).ToList();
    }

    public Task RemoveAsync(Guid uid, CancellationToken cancellationToken)
    {
        RequireWriteMode(nameof(RemoveAsync));

        return _boundConnection!.ExecuteAsync(new CommandDefinition(
            "DELETE FROM source_exclusions WHERE uid = @Uid AND is_system = 0;",
            new { Uid = uid.ToString() },
            _boundTransaction,
            cancellationToken: cancellationToken));
    }

    /// <summary>Same normalization rule <c>DiscoveryScanner</c> applies before prefix-matching a candidate path against these rows: trim trailing separators, uppercase drive letters, backslashes only.</summary>
    internal static string NormalizePath(string path) =>
        path.Replace('/', '\\').TrimEnd('\\').ToUpperInvariant();

    private void RequireWriteMode(string memberName)
    {
        if (_boundConnection is null)
        {
            throw new InvalidOperationException($"{nameof(SourceExclusionRepository)}.{memberName} must run inside IUnitOfWork.ExecuteAsync.");
        }
    }

    private static SourceExclusion Map(ExclusionRow row) => new(
        Guid.Parse(row.Uid), row.ExclusionType, row.Path, row.PathNormalized, row.IsSystem != 0, row.IsEnabled != 0,
        row.Reason, row.CreatedBy is null ? null : Guid.Parse(row.CreatedBy), DateTimeOffset.FromUnixTimeMilliseconds(row.CreatedAt));

    private sealed class ExclusionRow
    {
        public string Uid { get; set; } = "";
        public string ExclusionType { get; set; } = "";
        public string Path { get; set; } = "";
        public string PathNormalized { get; set; } = "";
        public long IsSystem { get; set; }
        public long IsEnabled { get; set; }
        public string? Reason { get; set; }
        public string? CreatedBy { get; set; }
        public long CreatedAt { get; set; }
    }
}
