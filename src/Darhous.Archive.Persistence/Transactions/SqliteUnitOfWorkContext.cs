using Microsoft.Data.Sqlite;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Persistence.Outbox;
using Darhous.Archive.Persistence.Repositories;

namespace Darhous.Archive.Persistence.Transactions;

/// <summary>Only valid for the duration of one write-queue callback — see <see cref="IUnitOfWorkContext"/>.</summary>
internal sealed class SqliteUnitOfWorkContext(SqliteConnection connection, SqliteTransaction transaction) : IUnitOfWorkContext
{
    public IRoleRepository Roles { get; } = new RoleRepository(connection, transaction);

    public IOutboxWriter Outbox { get; } = new OutboxWriter(connection, transaction);
}
