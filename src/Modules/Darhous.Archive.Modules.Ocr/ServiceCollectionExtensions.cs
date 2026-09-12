using Darhous.Archive.Core.Health;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Workers.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Darhous.Archive.Modules.Ocr;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOcrModule(
        this IServiceCollection services,
        OcrSupervisionOptions? options = null)
    {
        services.AddSingleton(options ?? new OcrSupervisionOptions());
        services.AddSingleton<IWorkerDelayProvider, DefaultWorkerDelayProvider>();
        services.AddSingleton<IManagedOcrProvider>(sp => new WorkerOcrProvider(
            sp.GetRequiredService<OcrSupervisionOptions>(),
            sp.GetRequiredService<IHealthRegistry>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<IWorkerDelayProvider>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()));
        services.AddSingleton<IOcrProvider>(sp => sp.GetRequiredService<IManagedOcrProvider>());
        services.AddHostedService(sp => sp.GetRequiredService<IManagedOcrProvider>());
        services.AddSingleton<OcrExtractionSweepService>();
        services.AddHostedService(sp => sp.GetRequiredService<OcrExtractionSweepService>());
        return services;
    }
}
