using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Core.Results;

namespace Darhous.Archive.Modules.Discovery.WatchFolders;

public interface IWatchFolderService
{
    Task<Result<Guid>> AddAsync(string path, bool includeSubfolders, string importMode, Guid? destinationFolderId, Guid? createdBy, CancellationToken cancellationToken);

    Task<IReadOnlyList<WatchFolder>> ListEnabledAsync(CancellationToken cancellationToken);
}
