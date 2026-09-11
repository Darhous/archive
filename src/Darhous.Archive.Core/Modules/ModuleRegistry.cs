using System.Collections.Concurrent;

namespace Darhous.Archive.Core.Modules;

public sealed class ModuleRegistry : IModuleRegistry
{
    private readonly ConcurrentDictionary<string, ModuleDescriptor> _modules = new();

    public void Register(IArchiveModule module)
    {
        ArgumentNullException.ThrowIfNull(module);

        if (!_modules.TryAdd(module.Descriptor.Id, module.Descriptor))
        {
            throw new InvalidOperationException($"Module '{module.Descriptor.Id}' is already registered.");
        }
    }

    public IReadOnlyCollection<ModuleDescriptor> RegisteredModules => _modules.Values.ToArray();
}
