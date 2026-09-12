using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Darhous.Archive.Workers.Protocol;
using Darhous.Archive.Workers.Transport;

namespace Darhous.Archive.Workers.Tests;

[SupportedOSPlatform("windows")]
public sealed class NamedPipeTransportTests
{
    [Fact]
    public async Task SameProcessServerAndClient_ExchangeFramedMessagesInBothDirections()
    {
        var options = CreateOptions();
        var pipeName = NamedPipeTransport.CreateUniquePipeName(options);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var server = NamedPipeTransport.CreateServer(pipeName, options);
        var connectionTask = NamedPipeTransport.ConnectClientAsync(pipeName, options, timeout.Token);
        await Task.WhenAll(server.WaitForConnectionAsync(timeout.Token), connectionTask);
        await using var client = await connectionTask;

        var receiveRequestTask = server.ReceiveAsync(timeout.Token).AsTask();
        await client.SendAsync(WorkerMessage.Create(1, "request", new PipePayload("hello")), timeout.Token);
        var request = await receiveRequestTask;

        var receiveResponseTask = client.ReceiveAsync(timeout.Token).AsTask();
        await server.SendAsync(WorkerMessage.Create(1, "response", new PipePayload("world")), timeout.Token);
        var response = await receiveResponseTask;

        Assert.True(server.IsConnected);
        Assert.True(client.IsConnected);
        Assert.Equal(new PipePayload("hello"), request.DeserializePayload<PipePayload>());
        Assert.Equal(new PipePayload("world"), response.DeserializePayload<PipePayload>());
    }

    [Fact]
    public async Task ServerAcl_IsProtectedAndAllowsOnlyCurrentUser()
    {
        var options = CreateOptions();
        var pipeName = NamedPipeTransport.CreateUniquePipeName(options);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var server = NamedPipeTransport.CreateServer(pipeName, options);
        using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous,
            TokenImpersonationLevel.Identification);

        await Task.WhenAll(
            server.WaitForConnectionAsync(timeout.Token),
            client.ConnectAsync(timeout.Token));

        using var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query);
        var currentUser = identity.User;
        Assert.NotNull(currentUser);

        var security = client.GetAccessControl();
        var rules = security
            .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .Cast<PipeAccessRule>()
            .ToArray();

        Assert.True(security.AreAccessRulesProtected);
        var rule = Assert.Single(rules);
        Assert.False(rule.IsInherited);
        Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        Assert.Equal(currentUser, rule.IdentityReference);
        Assert.Equal(PipeAccessRights.FullControl, rule.PipeAccessRights);
    }

    private static WorkerProtocolOptions CreateOptions() => new()
    {
        PipeNamePrefix = $"Darhous.Tests.{Guid.NewGuid():N}",
        HandshakeTimeout = TimeSpan.FromSeconds(5),
        ManagedTempStorageRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
    };

    private sealed record PipePayload(string Value);
}
