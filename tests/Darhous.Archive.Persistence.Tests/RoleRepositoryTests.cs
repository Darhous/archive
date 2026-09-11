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

        // "admin"/"user"/"readonly"/"guest" are seeded by M202609110003_SeedRoles (Phase 3) —
        // use a code that doesn't collide with those to test Create in isolation.
        var writeRepository = new RoleRepository(connection, transaction);
        var uid = await writeRepository.CreateAsync("custom-role", "Custom Role", isSystem: false, CancellationToken.None);
        await transaction.CommitAsync();

        var readRepository = new RoleRepository(new SqliteConnectionFactory(Options));
        var role = await readRepository.GetByCodeAsync("custom-role", CancellationToken.None);

        Assert.NotNull(role);
        Assert.Equal(uid, role!.Uid);
        Assert.Equal("Custom Role", role.DisplayName);
        Assert.False(role.IsSystem);
    }

    [Fact]
    public async Task GetByUid_Unknown_ReturnsNull()
    {
        var repository = new RoleRepository(new SqliteConnectionFactory(Options));

        var role = await repository.GetByUidAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(role);
    }

    [Fact]
    public async Task ListAsync_ReturnsSeededRolesPlusAnyCreated()
    {
        // The 4 system roles (admin/user/readonly/guest) already exist from
        // M202609110003_SeedRoles by the time PersistenceTestBase.InitializeAsync finishes —
        // this proves the seed migration ran, then proves Create adds a 5th on top of it.
        var readRepository = new RoleRepository(new SqliteConnectionFactory(Options));
        var seeded = await readRepository.ListAsync(CancellationToken.None);

        Assert.Equal(4, seeded.Count);
        Assert.Contains(seeded, r => r.Code == "admin");
        Assert.Contains(seeded, r => r.Code == "guest");

        await using var connection = await new SqliteConnectionFactory(Options).OpenAsync(DatabaseKind.Archive, CancellationToken.None);
        await using (var transaction = (SqliteTransaction)await connection.BeginTransactionAsync())
        {
            await new RoleRepository(connection, transaction).CreateAsync("custom-role", "Custom Role", false, CancellationToken.None);
            await transaction.CommitAsync();
        }

        var afterCreate = await readRepository.ListAsync(CancellationToken.None);
        Assert.Equal(5, afterCreate.Count);
    }
}
