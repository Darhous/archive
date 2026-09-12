using Darhous.Archive.Modules.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Darhous.Notifications.Windows;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsNotifications(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<INotificationProvider, WindowsToastNotificationProvider>());
        return services;
    }
}
