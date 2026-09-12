using Darhous.Archive.Contracts.Operations;

namespace Darhous.Archive.Application.Persistence;

public interface IOperationSnapshotRepository
{
    Task<Guid> CreateAsync(Guid? userId, string operationType, string payloadJson, DateTimeOffset expiresAt, CancellationToken cancellationToken);

    Task<OperationSnapshot?> GetByUidAsync(Guid uid, CancellationToken cancellationToken);

    Task MarkRevertedAsync(Guid uid, DateTimeOffset revertedAt, CancellationToken cancellationToken);
}
