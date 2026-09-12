namespace Darhous.Archive.PluginSdk.Configuration;

/// <summary>Plugin SDK §79 — the kinds of UI a plugin can extend. No host surface (Explorer/Settings) renders these yet; the registry exists so a plugin's declared extensions are captured now and a future UI phase can enumerate them without any plugin-side change.</summary>
public enum UiExtensionPoint
{
    SidebarPage,
    SettingsPage,
    ToolbarButton,
    ContextMenuCommand,
    DocumentTab,
    SearchProvider,
    ExportAction,
    Report,
    DeviceProvider,
}

public sealed record UiExtensionDescriptor(UiExtensionPoint Point, string Id, string DisplayName);

public interface IUiExtensionRegistry
{
    void Register(UiExtensionDescriptor descriptor);
}
