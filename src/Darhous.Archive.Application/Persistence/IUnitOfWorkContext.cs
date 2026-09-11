namespace Darhous.Archive.Application.Persistence;

/// <summary>
/// Repositories bound to the transaction of the <see cref="IUnitOfWork.ExecuteAsync{TResult}"/>
/// call currently in flight. Only valid for the duration of that callback — it runs on the
/// write queue's single worker, so nothing here should be captured and used later.
/// </summary>
public interface IUnitOfWorkContext
{
    IRoleRepository Roles { get; }

    IOutboxWriter Outbox { get; }
}
