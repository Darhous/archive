using Microsoft.Extensions.DependencyInjection;

namespace Darhous.Archive.Modules.Folders;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFoldersModule(this IServiceCollection services)
    {
        services.AddSingleton<IFolderService, FolderService>();
        return services;
    }
}
