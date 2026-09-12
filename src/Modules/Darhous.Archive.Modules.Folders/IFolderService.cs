using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Core.Results;

namespace Darhous.Archive.Modules.Folders;

public interface IFolderService
{
    Task<Result<Guid>> CreateFolderAsync(string name, Guid? parentId, Guid? createdBy, CancellationToken cancellationToken);

    Task<Result> RenameFolderAsync(Guid folderUid, string newName, CancellationToken cancellationToken);

    /// <summary>SAD §38 (Folder Move) — rejects cycles (moving a folder into itself or one of its own descendants).</summary>
    Task<Result> MoveFolderAsync(Guid folderUid, Guid? newParentId, CancellationToken cancellationToken);

    /// <summary>
    /// DB Spec §137 (Folder Delete) — blocks by default if the folder has subfolders or
    /// documents. <paramref name="moveContentsToUnclassified"/> reassigns direct child
    /// folders to root and documents to Unclassified (folder_id = NULL) before deleting.
    /// </summary>
    Task<Result> DeleteFolderAsync(Guid folderUid, bool moveContentsToUnclassified, CancellationToken cancellationToken);

    Task<IReadOnlyList<Folder>> GetChildrenAsync(Guid? parentId, CancellationToken cancellationToken);
}
