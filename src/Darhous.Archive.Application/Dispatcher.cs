using System.Reflection;
using Darhous.Archive.Contracts.Commands;
using Darhous.Archive.Contracts.Queries;
using Darhous.Archive.Core.Results;

namespace Darhous.Archive.Application;

/// <summary>
/// Resolves the registered <see cref="ICommandHandler{TCommand}"/>/<see cref="IQueryHandler{TQuery,TResult}"/>
/// for whatever concrete command/query is passed in, via reflection over the closed generic
/// handler type. No handlers exist yet (Documents/Folders/Search land in later phases) —
/// this only proves the wiring works end to end.
/// </summary>
public sealed class Dispatcher(IServiceProvider serviceProvider) : IDispatcher
{
    public Task<Result> SendAsync(ICommand command, CancellationToken cancellationToken) =>
        InvokeHandlerAsync<Result>(typeof(ICommandHandler<>), [command.GetType()], command, cancellationToken);

    public Task<Result<TResult>> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken) =>
        InvokeHandlerAsync<Result<TResult>>(typeof(ICommandHandler<,>), [command.GetType(), typeof(TResult)], command, cancellationToken);

    public Task<Result<TResult>> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken) =>
        InvokeHandlerAsync<Result<TResult>>(typeof(IQueryHandler<,>), [query.GetType(), typeof(TResult)], query, cancellationToken);

    private Task<TDispatchResult> InvokeHandlerAsync<TDispatchResult>(
        Type openHandlerType, Type[] genericArguments, object message, CancellationToken cancellationToken)
    {
        var handlerType = openHandlerType.MakeGenericType(genericArguments);
        var handler = serviceProvider.GetService(handlerType)
            ?? throw new InvalidOperationException(
                $"No handler of type {handlerType.Name} is registered for {message.GetType().Name}.");

        var method = handlerType.GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new MissingMethodException(handlerType.FullName, "HandleAsync");

        return (Task<TDispatchResult>)method.Invoke(handler, [message, cancellationToken])!;
    }
}
