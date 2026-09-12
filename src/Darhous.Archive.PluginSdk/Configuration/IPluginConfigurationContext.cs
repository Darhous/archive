using Darhous.Archive.Core.Health;

namespace Darhous.Archive.PluginSdk.Configuration;

/// <summary>Plugin SDK §21. <see cref="Health"/> is the host's real <c>IHealthRegistry</c> (Core) — a plugin's health contributor is folded into the same System Health page as every built-in module, not a parallel plugin-only health system.</summary>
public interface IPluginConfigurationContext
{
    IServiceRegistry Services { get; }
    IEventSubscriptionRegistry Events { get; }
    IUiExtensionRegistry Ui { get; }
    IBackgroundTaskRegistry BackgroundTasks { get; }
    IHealthRegistry Health { get; }
}
