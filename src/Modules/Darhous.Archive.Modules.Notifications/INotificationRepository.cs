using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace Darhous.Archive.Modules.Notifications;

public interface INotificationRepository
{
    Task<Notification> CreateAsync(Notification notification, CancellationToken cancellationToken);
    Task<IReadOnlyList<Notification>> ListAsync(bool unreadOnly, int limit, int offset, CancellationToken cancellationToken);
    Task MarkAsReadAsync(Guid uid, DateTimeOffset readAt, CancellationToken cancellationToken);
    Task DismissAsync(Guid uid, DateTimeOffset dismissedAt, CancellationToken cancellationToken);
}
