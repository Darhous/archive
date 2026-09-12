using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Darhous.Archive.Modules.Notifications;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services)
    {
        services.TryAddSingleton<INotificationRepository, NotificationRepository>();
        services.TryAddSingleton<INotificationService, NotificationService>();
        services.AddHostedService<NotificationSubscriptionService>();
        return services;
    }
}
