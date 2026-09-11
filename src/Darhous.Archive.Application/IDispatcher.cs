using Darhous.Archive.Contracts.Commands;
using Darhous.Archive.Contracts.Queries;
using Darhous.Archive.Core.Results;

namespace Darhous.Archive.Application;

/// <summary>
/// Single entry point the Desktop shell / Workers use to run a Use Case, without knowing
/// which handler implements it (Implementation Plan §9 — Application skeleton).
/// </summary>
public interface IDispatcher
{
    Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken);

    Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken);

    Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken);
}
