namespace Darhous.Archive.PluginSdk.Runtime;

/// <summary>
/// What a running plugin can resolve — its own services registered via
/// <see cref="Configuration.IServiceRegistry"/> during <c>ConfigureAsync</c>, PLUS the
/// official host services (Plugin SDK §23: IDocumentService, IFolderService, ISearchService,
/// IUserContext, IAuditService, IEventBus, ...) that the host explicitly exposes. A plugin can
/// never resolve an arbitrary host-internal type — only whatever the host chose to hand it.
/// </summary>
public interface IServiceResolver
{
    T GetRequiredService<T>() where T : class;

    T? GetService<T>() where T : class;
}
