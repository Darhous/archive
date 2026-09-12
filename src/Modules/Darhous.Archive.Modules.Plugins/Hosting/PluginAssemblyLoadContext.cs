using System.Reflection;
using System.Runtime.Loader;

namespace Darhous.Archive.Modules.Plugins.Hosting;

/// <summary>
/// Collectible per-plugin load context (Plugin SDK §80 "In-Process" isolation — the only mode
/// this phase implements; Out-of-Process needs Worker Infrastructure from Phase 13). Resolves
/// a plugin's own dependency DLLs from its own bin directory first, so two plugins can each
/// ship a different version of the same third-party library without colliding — and falls
/// back to the default context for anything the plugin shares with the host (the PluginSdk
/// assembly itself, in particular, MUST resolve to the host's copy so an
/// <c>IArchivePlugin</c> instance is actually assignable to the host's interface type).
///
/// Every load goes through <see cref="LoadFromFile"/> (reads bytes into memory, then
/// <c>LoadFromStream</c>) rather than <c>LoadFromAssemblyPath</c> — the latter keeps the file
/// memory-mapped, and therefore locked on Windows, for as long as this context is alive.
/// Collectible-context unload is GC-driven and not immediate regardless, but a memory-mapped
/// file lock is a hard blocker for Remove/Update deleting or overwriting installed files, not
/// just a timing race.
/// </summary>
public sealed class PluginAssemblyLoadContext(string entryAssemblyPath)
    : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(entryAssemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is not null ? LoadFromFile(path) : null;
    }

    public Assembly LoadFromFile(string assemblyPath) => LoadFromStream(new MemoryStream(File.ReadAllBytes(assemblyPath)));
}
