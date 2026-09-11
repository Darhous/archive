using Microsoft.Extensions.DependencyInjection;

namespace Darhous.Archive.Core.Modules;

/// <summary>
/// Contract every official Module implements (Plugin SDK's <c>IArchiveModule</c>).
/// A full third-party Plugin Host (signature verification, isolation, permissions) is
/// built in Phase 12 — for now this only lets Core discover and wire up its own
/// in-process Modules through one uniform registration point.
/// </summary>
public interface IArchiveModule
{
    ModuleDescriptor Descriptor { get; }

    /// <summary>Registers the module's services into the host's DI container.</summary>
    void RegisterServices(IServiceCollection services);
}
