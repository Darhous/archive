namespace Darhous.Archive.PluginSdk.Runtime;

/// <summary>Plugin SDK §22.</summary>
public interface IPluginRuntimeContext
{
    IServiceResolver Services { get; }
    IPluginStorage Storage { get; }
    IPluginSecrets Secrets { get; }
    IPluginLogger Logger { get; }
    IPluginEnvironment Environment { get; }
}
