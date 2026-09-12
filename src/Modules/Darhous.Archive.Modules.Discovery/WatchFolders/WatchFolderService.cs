using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Core.Results;

namespace Darhous.Archive.Modules.Discovery.WatchFolders;

public sealed class WatchFolderService(IUnitOfWork unitOfWork) : IWatchFolderService
{
    public async Task<Result<Guid>> AddAsync(
        string path, bool includeSubfolders, string importMode, Guid? destinationFolderId, Guid? createdBy, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(path))
        {
            return Result<Guid>.Failure(Error.Of("WATCH_FOLDER_NOT_FOUND", "المجلد المحدد غير موجود."));
        }

        var existing = await unitOfWork.ExecuteAsync((context, ct) => context.WatchFolders.GetByPathAsync(path, ct), cancellationToken);
        if (existing is not null)
        {
            return Result<Guid>.Failure(Error.Of("WATCH_FOLDER_ALREADY_ADDED", "هذا المجلد مضاف بالفعل."));
        }

        var uid = await unitOfWork.ExecuteAsync(
            (context, ct) => context.WatchFolders.CreateAsync(new NewWatchFolder(path, includeSubfolders, importMode, destinationFolderId, createdBy), ct),
            cancellationToken);

        return Result<Guid>.Success(uid);
    }

    public Task<IReadOnlyList<WatchFolder>> ListEnabledAsync(CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync((context, ct) => context.WatchFolders.ListEnabledAsync(ct), cancellationToken);
}
