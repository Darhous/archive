using Darhous.Archive.PluginSdk.Configuration;
using Darhous.Archive.PluginSdk.Runtime;

namespace Darhous.Archive.PluginSdk;

/// <summary>
/// Plugin SDK §16-20. <see cref="ConfigureAsync"/> registers services/events/UI extensions
/// only — no network calls, no long jobs, no scanner access, no user-data mutation.
/// <see cref="StartAsync"/> may start a registered background service or warm a cache, but
/// must never block the UI thread or spawn unmanaged threads. <see cref="StopAsync"/> must
/// respect the cancellation token and release everything it opened.
/// </summary>
public interface IArchivePlugin
{
    PluginIdentity Identity { get; }

    ValueTask ConfigureAsync(IPluginConfigurationContext context, CancellationToken cancellationToken);

    ValueTask StartAsync(IPluginRuntimeContext context, CancellationToken cancellationToken);

    ValueTask StopAsync(CancellationToken cancellationToken);
}
