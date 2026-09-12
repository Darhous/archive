using Microsoft.Extensions.DependencyInjection;
using Darhous.Archive.Modules.Documents.BulkOperations;
using Darhous.Archive.Modules.Documents.Services;
using Darhous.Archive.Modules.Documents.Storage;

namespace Darhous.Archive.Modules.Documents;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentsModule(this IServiceCollection services, DocumentStorageOptions? options = null)
    {
        services.AddSingleton(options ?? new DocumentStorageOptions());
        services.AddSingleton<IFileStorageService, FileStorageService>();
        services.AddSingleton<IDocumentService, DocumentService>();
        services.AddSingleton<IBulkOperationService, BulkOperationService>();

        return services;
    }
}
