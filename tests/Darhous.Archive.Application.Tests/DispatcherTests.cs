using Darhous.Archive.Contracts.Commands;
using Darhous.Archive.Contracts.Queries;
using Darhous.Archive.Core.Results;
using Microsoft.Extensions.DependencyInjection;

namespace Darhous.Archive.Application.Tests;

public class DispatcherTests
{
    private sealed record Ping : ICommand<string>;

    private sealed class PingHandler : ICommandHandler<Ping, string>
    {
        public Task<Result<string>> HandleAsync(Ping command, CancellationToken cancellationToken) =>
            Task.FromResult(Result<string>.Success("pong"));
    }

    private sealed record GetGreeting(string Name) : IQuery<string>;

    private sealed class GetGreetingHandler : IQueryHandler<GetGreeting, string>
    {
        public Task<Result<string>> HandleAsync(GetGreeting query, CancellationToken cancellationToken) =>
            Task.FromResult(Result<string>.Success($"Hello, {query.Name}"));
    }

    private static IDispatcher BuildDispatcher(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        services.AddApplicationLayer();
        register(services);
        return services.BuildServiceProvider().GetRequiredService<IDispatcher>();
    }

    [Fact]
    public async Task SendAsync_Generic_ResolvesRegisteredHandler()
    {
        var dispatcher = BuildDispatcher(s => s.AddTransient<ICommandHandler<Ping, string>, PingHandler>());

        var result = await dispatcher.SendAsync(new Ping(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("pong", result.Value);
    }

    [Fact]
    public async Task QueryAsync_ResolvesRegisteredHandler()
    {
        var dispatcher = BuildDispatcher(s => s.AddTransient<IQueryHandler<GetGreeting, string>, GetGreetingHandler>());

        var result = await dispatcher.QueryAsync<string>(new GetGreeting("Ahmed"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Hello, Ahmed", result.Value);
    }

    [Fact]
    public async Task SendAsync_NoHandlerRegistered_Throws()
    {
        var dispatcher = BuildDispatcher(_ => { });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.SendAsync(new Ping(), CancellationToken.None));
    }
}
