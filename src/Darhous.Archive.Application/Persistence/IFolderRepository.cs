namespace Darhous.Archive.Application.Persistence;

public interface IFolderRepository
{
    Task<Guid> CreateAsync(NewFolder folder, CancellationToken cancellationToken);

    Task<Folder?> GetByUidAsync(Guid uid, CancellationToken cancellationToken);

    Task<IReadOnlyList<Folder>> ListChildrenAsync(Guid? parentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Folder>> ListAllAsync(CancellationToken cancellationToken);

    Task RenameAsync(Guid folderUid, string newName, CancellationToken cancellationToken);

    Task MoveAsync(Guid folderUid, Guid? newParentId, CancellationToken cancellationToken);

    /// <summary>Fails at the DB level (ON DELETE RESTRICT) if the folder still has subfolders — caller must empty it first.</summary>
    Task DeleteAsync(Guid folderUid, CancellationToken cancellationToken);

    Task<int> CountDocumentsAsync(Guid folderUid, CancellationToken cancellationToken);

    Task<bool> HasChildrenAsync(Guid folderUid, CancellationToken cancellationToken);
}
