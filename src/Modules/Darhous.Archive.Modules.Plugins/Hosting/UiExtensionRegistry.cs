using Darhous.Archive.PluginSdk.Configuration;

namespace Darhous.Archive.Modules.Plugins.Hosting;

public sealed class UiExtensionRegistry : IUiExtensionRegistry
{
    private readonly List<UiExtensionDescriptor> _descriptors = [];

    public IReadOnlyList<UiExtensionDescriptor> Descriptors => _descriptors;

    public void Register(UiExtensionDescriptor descriptor) => _descriptors.Add(descriptor);
}
