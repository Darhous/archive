using Darhous.Archive.Contracts.Queries;
using Darhous.Archive.Core.Results;

namespace Darhous.Archive.Application;

public interface IQueryHandler<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
