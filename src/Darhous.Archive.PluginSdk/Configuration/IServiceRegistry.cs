namespace Darhous.Archive.PluginSdk.Configuration;

/// <summary>
/// A plugin's OWN private service container — not the host's global DI container. Letting a
/// plugin register into the host container would mean one plugin's bad registration (or a
/// malicious one) could shadow a host service every other component resolves; this keeps
/// each plugin's registrations visible only to itself, resolved back via
/// <c>IPluginRuntimeContext.Services</c> (<see cref="Runtime.IServiceResolver"/>).
/// </summary>
public interface IServiceRegistry
{
    void AddSingleton<TService>(TService instance) where TService : class;

    void AddSingleton<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService;
}
