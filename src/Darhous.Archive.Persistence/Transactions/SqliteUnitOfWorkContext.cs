using Microsoft.Data.Sqlite;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Persistence.Outbox;
using Darhous.Archive.Persistence.Repositories;

namespace Darhous.Archive.Persistence.Transactions;

/// <summary>Only valid for the duration of one write-queue callback — see <see cref="IUnitOfWorkContext"/>.</summary>
internal sealed class SqliteUnitOfWorkContext(SqliteConnection connection, SqliteTransaction transaction) : IUnitOfWorkContext
{
    public IRoleRepository Roles { get; } = new RoleRepository(connection, transaction);

    public IAppUserRepository Users { get; } = new AppUserRepository(connection, transaction);

    public ISessionRepository Sessions { get; } = new SessionRepository(connection, transaction);

    public IDocumentRepository Documents { get; } = new DocumentRepository(connection, transaction);

    public IDocumentVersionRepository DocumentVersions { get; } = new DocumentVersionRepository(connection, transaction);

    public IRecycleBinRepository RecycleBin { get; } = new RecycleBinRepository(connection, transaction);

    public IArchiveNumberGenerator ArchiveNumbers { get; } = new ArchiveNumberGenerator(connection, transaction);

    public IFolderRepository Folders { get; } = new FolderRepository(connection, transaction);

    public IOperationSnapshotRepository OperationSnapshots { get; } = new OperationSnapshotRepository(connection, transaction);

    public IJobRepository Jobs { get; } = new JobRepository(connection, transaction);

    public IWatchFolderRepository WatchFolders { get; } = new WatchFolderRepository(connection, transaction);

    public ISourceExclusionRepository SourceExclusions { get; } = new SourceExclusionRepository(connection, transaction);

    public ISourceDriveRepository SourceDrives { get; } = new SourceDriveRepository(connection, transaction);

    public IDiscoveryRunRepository DiscoveryRuns { get; } = new DiscoveryRunRepository(connection, transaction);

    public IOutboxWriter Outbox { get; } = new OutboxWriter(connection, transaction);
}
