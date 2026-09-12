namespace Darhous.Archive.PluginSdk.Runtime;

/// <summary>Plugin SDK §26 — the only sanctioned way for a plugin to touch the filesystem; it never sees a path outside its own <c>PluginData\&lt;PluginId&gt;\</c> folder.</summary>
public interface IPluginStorage
{
    string RootDirectory { get; }

    string GetPath(string relativePath);
}
