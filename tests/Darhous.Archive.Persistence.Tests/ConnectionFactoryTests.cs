using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;

namespace Darhous.Archive.Persistence.Tests;

public class ConnectionFactoryTests : PersistenceTestBase
{
    [Theory]
    [InlineData(DatabaseKind.Archive)]
    [InlineData(DatabaseKind.Audit)]
    [InlineData(DatabaseKind.Search)]
    public async Task OpenAsync_CreatesFileWithWalAndForeignKeysEnabled(DatabaseKind database)
    {
        var factory = new SqliteConnectionFactory(Options);

        await using var connection = await factory.OpenAsync(database, CancellationToken.None);

        Assert.True(File.Exists(Options.GetFilePath(database)));

        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode;";
            var mode = (string)(await pragma.ExecuteScalarAsync())!;
            Assert.Equal("wal", mode, ignoreCase: true);
        }

        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys;";
            var enabled = (long)(await pragma.ExecuteScalarAsync())!;
            Assert.Equal(1, enabled);
        }
    }

    [Fact]
    public async Task OpenAsync_TwoConnections_CanReadConcurrently()
    {
        var factory = new SqliteConnectionFactory(Options);

        await using var first = await factory.OpenAsync(DatabaseKind.Archive, CancellationToken.None);
        await using var second = await factory.OpenAsync(DatabaseKind.Archive, CancellationToken.None);

        await using var cmd1 = first.CreateCommand();
        cmd1.CommandText = "SELECT COUNT(*) FROM roles;";
        await using var cmd2 = second.CreateCommand();
        cmd2.CommandText = "SELECT COUNT(*) FROM roles;";

        // Both succeed without SQLITE_BUSY — WAL allows concurrent readers.
        await cmd1.ExecuteScalarAsync();
        await cmd2.ExecuteScalarAsync();
    }
}
