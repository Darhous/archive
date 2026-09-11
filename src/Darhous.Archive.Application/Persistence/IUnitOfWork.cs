namespace Darhous.Archive.Application.Persistence;

/// <summary>
/// One atomic write against archive.db. Operation-based (not an owned <c>IDbTransaction</c>
/// handed to the caller) so a transaction can never be left open or committed twice —
/// the delegate's return value is the only thing that survives past the call.
/// Implemented by Persistence as a thin adapter over the serialized write queue
/// (Phase 2 design review — see docs/EXECUTION_PLAN.md §5 Phase 2 entry).
/// </summary>
public interface IUnitOfWork
{
    Task<TResult> ExecuteAsync<TResult>(
        Func<IUnitOfWorkContext, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}
