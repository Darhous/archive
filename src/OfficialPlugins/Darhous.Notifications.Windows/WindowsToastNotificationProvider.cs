using System.Threading;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Notifications;
using Darhous.Archive.Modules.Notifications;

namespace Darhous.Notifications.Windows;

public sealed class WindowsToastNotificationProvider : INotificationProvider
{
    public Task ShowAsync(Notification notification, CancellationToken cancellationToken)
    {
        var builder = new ToastContentBuilder()
            .AddText(notification.Title)
            .AddText(notification.Body);

        // Map severity to visual hint if desired, or skip it.
        // E.g., if it's an error, we might use a different audio or something, but defaults are fine.

        if (!string.IsNullOrEmpty(notification.ActionJson))
        {
            // Simple mapping: assume ActionJson contains an action name/arguments.
            // For now, we just pass the raw json as an argument.
            builder.AddArgument("action", notification.ActionJson);
        }

        builder.Show();
        return Task.CompletedTask;
    }
}
