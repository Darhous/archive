using Darhous.Archive.PluginSdk.Runtime;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Modules.Plugins.Hosting;

public sealed class PluginLogger(string pluginId, ILogger logger) : IPluginLogger
{
    public void LogInformation(string message) => logger.LogInformation("[{PluginId}] {Message}", pluginId, message);

    public void LogWarning(string message) => logger.LogWarning("[{PluginId}] {Message}", pluginId, message);

    public void LogError(Exception? exception, string message) => logger.LogError(exception, "[{PluginId}] {Message}", pluginId, message);
}
