using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Darhous.Archive.Modules.Ai;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiModule(
        this IServiceCollection services,
        AiWorkerOptions? workerOptions = null)
    {
        services.TryAddSingleton<IAiProviderRegistry>(sp =>
            new AiProviderRegistry(sp.GetServices<IAiProvider>()));
        services.TryAddSingleton<IAiSettingsService, AiSettingsService>();
        services.TryAddSingleton<IAiSecretStore, AiSecretStore>();
        services.TryAddSingleton(workerOptions ?? new AiWorkerOptions());
        services.TryAddSingleton<AiWorker>();
        services.TryAddSingleton<IAiWorker>(sp => sp.GetRequiredService<AiWorker>());
        services.TryAddSingleton<IAiAssistService, AiAssistService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<AiWorker>());
        return services;
    }
}
