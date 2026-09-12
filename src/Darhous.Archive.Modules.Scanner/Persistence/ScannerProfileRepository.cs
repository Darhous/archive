using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;

namespace Darhous.Archive.Modules.Scanner.Persistence;

public sealed class ScannerProfileRepository : IScannerProfileRepository
{
    private readonly ISqliteConnectionFactory _connectionFactory;

    public ScannerProfileRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ScannerProfile>> ListAllAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, name, resolution, color_mode, duplex, source, page_separation_mode, is_seed FROM scanner_profiles;";
        await using var connection = await _connectionFactory.OpenAsync(DatabaseKind.Archive, cancellationToken);
        var rows = await connection.QueryAsync<ProfileRow>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        return rows.Select(r => new ScannerProfile(
            (int)r.id, r.name, (int)r.resolution, r.color_mode, r.duplex == 1, r.source, r.page_separation_mode, r.is_seed == 1
        )).ToList();
    }

    private sealed class ProfileRow
    {
        public long id { get; set; }
        public string name { get; set; } = "";
        public long resolution { get; set; }
        public string color_mode { get; set; } = "";
        public long duplex { get; set; }
        public string source { get; set; } = "";
        public string page_separation_mode { get; set; } = "";
        public long is_seed { get; set; }
    }
}
