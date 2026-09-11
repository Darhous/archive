using Darhous.Archive.Contracts.Events;
using Darhous.Archive.Core.Events;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darhous.Archive.Core.Tests;

public class InMemoryEventBusTests
{
    [Fact]
    public async Task PublishAsync_DeliversToSubscriber()
    {
        var bus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);
        ArchiveEventEnvelope<string>? received = null;

        using var subscription = bus.Subscribe<string>((envelope, _) =>
        {
            received = envelope;
            return Task.CompletedTask;
        });

        await bus.PublishAsync(
            CoreEventTypes.ApplicationStarted, "payload", EventDeliveryLevel.Transient,
            correlationId: null, userId: null, CancellationToken.None);

        Assert.NotNull(received);
        Assert.Equal("payload", received!.Payload);
        Assert.Equal(CoreEventTypes.ApplicationStarted, received.EventType);
    }

    [Fact]
    public async Task PublishAsync_Reliable_ThrowsUntilOutboxExists()
    {
        var bus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            bus.PublishAsync(
                "SomeEvent", "payload", EventDeliveryLevel.Reliable,
                correlationId: null, userId: null, CancellationToken.None));
    }

    [Fact]
    public async Task Dispose_StopsFurtherDelivery()
    {
        var bus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);
        var callCount = 0;

        var subscription = bus.Subscribe<string>((_, _) =>
        {
            callCount++;
            return Task.CompletedTask;
        });
        subscription.Dispose();

        await bus.PublishAsync(
            "SomeEvent", "payload", EventDeliveryLevel.Transient,
            correlationId: null, userId: null, CancellationToken.None);

        Assert.Equal(0, callCount);
    }

    [Fact]
    public async Task PublishAsync_OneSubscriberThrowing_DoesNotBlockOthers()
    {
        var bus = new InMemoryEventBus(NullLogger<InMemoryEventBus>.Instance);
        var secondCalled = false;

        using var first = bus.Subscribe<string>((_, _) => throw new InvalidOperationException("boom"));
        using var second = bus.Subscribe<string>((_, _) =>
        {
            secondCalled = true;
            return Task.CompletedTask;
        });

        await bus.PublishAsync(
            "SomeEvent", "payload", EventDeliveryLevel.Transient,
            correlationId: null, userId: null, CancellationToken.None);

        Assert.True(secondCalled);
    }
}
