using Darhous.Archive.PluginSdk.Configuration;
using Darhous.Archive.PluginSdk.Runtime;

namespace Darhous.Archive.Modules.Plugins.Hosting;

/// <summary>Backs both <see cref="IServiceRegistry"/> (write, during Configure) and <see cref="IServiceResolver"/> (read, during Start) — the same small per-plugin container for its whole lifetime.</summary>
public sealed class ServiceRegistry : IServiceRegistry, IServiceResolver
{
    private readonly Dictionary<Type, object> _instances = [];
    private readonly Dictionary<Type, Func<object>> _factories = [];

    public void AddSingleton<TService>(TService instance) where TService : class => _instances[typeof(TService)] = instance;

    public void AddSingleton<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService =>
        _factories[typeof(TService)] = () => Activator.CreateInstance<TImplementation>();

    public T GetRequiredService<T>() where T : class =>
        GetService<T>() ?? throw new InvalidOperationException($"No service of type '{typeof(T).Name}' has been registered.");

    public T? GetService<T>() where T : class
    {
        if (_instances.TryGetValue(typeof(T), out var instance))
        {
            return (T)instance;
        }

        if (_factories.TryGetValue(typeof(T), out var factory))
        {
            var created = factory();
            _instances[typeof(T)] = created;
            return (T)created;
        }

        return null;
    }
}
