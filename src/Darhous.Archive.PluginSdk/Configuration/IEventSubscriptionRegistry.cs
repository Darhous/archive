using Darhous.Archive.Contracts.Events;

namespace Darhous.Archive.PluginSdk.Configuration;

/// <summary>
/// Tracked wrapper over the host's <c>IEventBus</c> — every subscription made through here is
/// automatically disposed when the plugin stops, so a plugin can never leak a subscription
/// that outlives it (which would otherwise keep firing into a torn-down plugin instance).
/// </summary>
public interface IEventSubscriptionRegistry
{
    void Subscribe<T>(Func<ArchiveEventEnvelope<T>, CancellationToken, Task> handler);
}
