using Darhous.Archive.PluginSdk.Runtime;

namespace Darhous.Archive.Modules.Plugins.Hosting;

public sealed class PluginStorage(string rootDirectory) : IPluginStorage
{
    public string RootDirectory { get; } = rootDirectory;

    public string GetPath(string relativePath)
    {
        Directory.CreateDirectory(RootDirectory);
        var fullPath = Path.GetFullPath(Path.Combine(RootDirectory, relativePath));

        if (!fullPath.StartsWith(Path.GetFullPath(RootDirectory), StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException($"Path '{relativePath}' resolves outside the plugin's storage directory.");
        }

        return fullPath;
    }
}
