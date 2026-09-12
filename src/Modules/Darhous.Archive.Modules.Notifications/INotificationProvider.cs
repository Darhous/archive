using System.Threading;
using System.Threading.Tasks;

namespace Darhous.Archive.Modules.Notifications;

public interface INotificationProvider
{
    Task ShowAsync(Notification notification, CancellationToken cancellationToken);
}
