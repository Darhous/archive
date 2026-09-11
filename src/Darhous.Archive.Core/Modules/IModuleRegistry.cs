namespace Darhous.Archive.Core.Modules;

/// <summary>
/// Tracks which modules are known to the running host (SAD §9 — module states shown under
/// Settings → Modules &amp; Plugins).
/// </summary>
public interface IModuleRegistry
{
    void Register(IArchiveModule module);

    IReadOnlyCollection<ModuleDescriptor> RegisteredModules { get; }
}
