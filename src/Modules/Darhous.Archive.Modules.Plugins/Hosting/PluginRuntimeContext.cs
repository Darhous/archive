using Darhous.Archive.PluginSdk.Runtime;

namespace Darhous.Archive.Modules.Plugins.Hosting;

public sealed class PluginRuntimeContext(
    IServiceResolver services, IPluginStorage storage, IPluginSecrets secrets, IPluginLogger logger, IPluginEnvironment environment)
    : IPluginRuntimeContext
{
    public IServiceResolver Services { get; } = services;
    public IPluginStorage Storage { get; } = storage;
    public IPluginSecrets Secrets { get; } = secrets;
    public IPluginLogger Logger { get; } = logger;
    public IPluginEnvironment Environment { get; } = environment;
}
