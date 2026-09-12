namespace Darhous.Archive.Application.Persistence;

public interface IWatchFolderRepository
{
    Task<Guid> CreateAsync(NewWatchFolder folder, CancellationToken cancellationToken);

    Task<IReadOnlyList<WatchFolder>> ListEnabledAsync(CancellationToken cancellationToken);

    Task<WatchFolder?> GetByPathAsync(string path, CancellationToken cancellationToken);

    Task SetLastReconciledAsync(Guid uid, DateTimeOffset reconciledAt, CancellationToken cancellationToken);

    Task DisableAsync(Guid uid, CancellationToken cancellationToken);
}
