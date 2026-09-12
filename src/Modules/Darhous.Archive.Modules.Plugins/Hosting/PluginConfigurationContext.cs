using Darhous.Archive.Core.Health;
using Darhous.Archive.PluginSdk.Configuration;

namespace Darhous.Archive.Modules.Plugins.Hosting;

public sealed class PluginConfigurationContext(
    IServiceRegistry services, IEventSubscriptionRegistry events, IUiExtensionRegistry ui,
    IBackgroundTaskRegistry backgroundTasks, IHealthRegistry health)
    : IPluginConfigurationContext
{
    public IServiceRegistry Services { get; } = services;
    public IEventSubscriptionRegistry Events { get; } = events;
    public IUiExtensionRegistry Ui { get; } = ui;
    public IBackgroundTaskRegistry BackgroundTasks { get; } = backgroundTasks;
    public IHealthRegistry Health { get; } = health;
}
