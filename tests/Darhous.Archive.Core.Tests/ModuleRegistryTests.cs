using Darhous.Archive.Core.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace Darhous.Archive.Core.Tests;

public class ModuleRegistryTests
{
    private sealed class FakeModule(string id) : IArchiveModule
    {
        public ModuleDescriptor Descriptor { get; } = new(id, id, new Version(1, 0, 0));

        public void RegisterServices(IServiceCollection services)
        {
        }
    }

    [Fact]
    public void Register_AddsModule()
    {
        var registry = new ModuleRegistry();

        registry.Register(new FakeModule("Darhous.Archive.Modules.Documents"));

        Assert.Single(registry.RegisteredModules);
    }

    [Fact]
    public void Register_DuplicateId_Throws()
    {
        var registry = new ModuleRegistry();
        registry.Register(new FakeModule("dup"));

        Assert.Throws<InvalidOperationException>(() => registry.Register(new FakeModule("dup")));
    }
}
