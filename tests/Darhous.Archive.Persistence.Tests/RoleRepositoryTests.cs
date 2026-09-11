using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Repositories;
using Microsoft.Data.Sqlite;

namespace Darhous.Archive.Persistence.Tests;

public class RoleRepositoryTests : PersistenceTestBase
{
    [Fact]
    public async Task CreateAsync_OutsideTransaction_Throws()
    {
        var repository = new RoleRepository(new SqliteConnectionFactory(Options));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.CreateAsync("admin", "Admin", isSystem: true, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_ThenGetByCode_RoundTrips()
    {
        await using var connection = await new SqliteConnectionFactory(Options).OpenAsync(DatabaseKind.Archive, CancellationToken.None);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

        var writeRepository = new RoleRepository(connection, transaction);
        var uid = await writeRepository.CreateAsync("admin", "Admin", isSystem: true, CancellationToken.None);
        await transaction.CommitAsync();

        var readRepository = new RoleRepository(new SqliteConnectionFactory(Options));
        var role = await readRepository.GetByCodeAsync("admin", CancellationToken.None);

        Assert.NotNull(role);
        Assert.Equal(uid, role!.Uid);
        Assert.Equal("Admin", role.DisplayName);
        Assert.True(role.IsSystem);
    }

    [Fact]
    public async Task GetByUid_Unknown_ReturnsNull()
    {
        var repository = new RoleRepository(new SqliteConnectionFactory(Options));

        var role = await repository.GetByUidAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(role);
    }

    [Fact]
    public async Task ListAsync_ReturnsAllSeededRoles()
    {
        await using var connection = await new SqliteConnectionFactory(Options).OpenAsync(DatabaseKind.Archive, CancellationToken.None);
        await using (var transaction = (SqliteTransaction)await connection.BeginTransactionAsync())
        {
            var repo = new RoleRepository(connection, transaction);
            await repo.CreateAsync("admin", "Admin", true, CancellationToken.None);
            await repo.CreateAsync("user", "User", true, CancellationToken.None);
            await repo.CreateAsync("readonly", "Read Only", true, CancellationToken.None);
            await repo.CreateAsync("guest", "Guest", true, CancellationToken.None);
            await transaction.CommitAsync();
        }

        var readRepository = new RoleRepository(new SqliteConnectionFactory(Options));
        var roles = await readRepository.ListAsync(CancellationToken.None);

        Assert.Equal(4, roles.Count);
        Assert.Contains(roles, r => r.Code == "admin");
        Assert.Contains(roles, r => r.Code == "guest");
    }
}
