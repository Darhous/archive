namespace Darhous.Archive.Application.Persistence;

public interface IRecycleBinRepository
{
    Task AddAsync(RecycleBinEntry entry, CancellationToken cancellationToken);

    Task RemoveAsync(Guid documentUid, CancellationToken cancellationToken);

    Task<RecycleBinEntry?> GetAsync(Guid documentUid, CancellationToken cancellationToken);
}
