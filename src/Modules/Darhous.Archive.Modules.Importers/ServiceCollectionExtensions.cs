using Darhous.Archive.Modules.Importers.Extractors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Darhous.Archive.Modules.Importers;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddImportersModule(this IServiceCollection services)
    {
        services.AddSingleton<IContentExtractor, PdfContentExtractor>();
        services.AddSingleton<IContentExtractor, OpenXmlContentExtractor>();
        services.AddSingleton<IContentExtractor, EmlContentExtractor>();
        services.AddSingleton<IContentExtractor, MsgContentExtractor>();

        services.AddSingleton<TextExtractionSweepService>();
        services.AddHostedService(sp => sp.GetRequiredService<TextExtractionSweepService>());

        return services;
    }
}
