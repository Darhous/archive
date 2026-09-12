namespace Darhous.Archive.Application.Persistence;

/// <summary>
/// Repositories bound to the transaction of the <see cref="IUnitOfWork.ExecuteAsync{TResult}"/>
/// call currently in flight. Only valid for the duration of that callback — it runs on the
/// write queue's single worker, so nothing here should be captured and used later.
/// </summary>
public interface IUnitOfWorkContext
{
    IRoleRepository Roles { get; }

    IAppUserRepository Users { get; }

    ISessionRepository Sessions { get; }

    IDocumentRepository Documents { get; }

    IDocumentVersionRepository DocumentVersions { get; }

    IRecycleBinRepository RecycleBin { get; }

    IArchiveNumberGenerator ArchiveNumbers { get; }

    IFolderRepository Folders { get; }

    IOperationSnapshotRepository OperationSnapshots { get; }

    IJobRepository Jobs { get; }

    IWatchFolderRepository WatchFolders { get; }

    ISourceExclusionRepository SourceExclusions { get; }

    ISourceDriveRepository SourceDrives { get; }

    IDiscoveryRunRepository DiscoveryRuns { get; }

    IPluginRegistryRepository Plugins { get; }

    IOutboxWriter Outbox { get; }
}
