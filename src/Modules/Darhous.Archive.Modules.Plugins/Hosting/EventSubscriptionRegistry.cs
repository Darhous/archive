using Darhous.Archive.Contracts.Events;
using Darhous.Archive.Core.Events;
using Darhous.Archive.PluginSdk.Configuration;

namespace Darhous.Archive.Modules.Plugins.Hosting;

public sealed class EventSubscriptionRegistry(IEventBus eventBus) : IEventSubscriptionRegistry, IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];

    public void Subscribe<T>(Func<ArchiveEventEnvelope<T>, CancellationToken, Task> handler) =>
        _subscriptions.Add(eventBus.Subscribe(handler));

    public void Dispose()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
    }
}
