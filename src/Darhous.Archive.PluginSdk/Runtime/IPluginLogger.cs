namespace Darhous.Archive.PluginSdk.Runtime;

/// <summary>A plugin logs through this rather than taking a direct Serilog/ILogger dependency, so every plugin's log lines are automatically tagged with its plugin id.</summary>
public interface IPluginLogger
{
    void LogInformation(string message);

    void LogWarning(string message);

    void LogError(Exception? exception, string message);
}
