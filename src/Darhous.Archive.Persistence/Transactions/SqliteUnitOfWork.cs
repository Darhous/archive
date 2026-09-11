using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Persistence.Writes;

namespace Darhous.Archive.Persistence.Transactions;

/// <summary>Thin adapter: the "unit of work" is literally one item on the archive write queue.</summary>
public sealed class SqliteUnitOfWork(ISqliteWriteQueue writeQueue) : IUnitOfWork
{
    public Task<TResult> ExecuteAsync<TResult>(
        Func<IUnitOfWorkContext, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken) =>
        writeQueue.EnqueueAsync(
            (connection, transaction, ct) => operation(new SqliteUnitOfWorkContext(connection, transaction), ct),
            cancellationToken);
}
