using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Darhous.Archive.Modules.Notifications;

public interface INotificationService
{
    Task<Notification> CreateAsync(string type, string severity, string title, string body, string source, string? actionJson, CancellationToken cancellationToken);
    Task<IReadOnlyList<Notification>> ListAsync(bool unreadOnly, int limit, int offset, CancellationToken cancellationToken);
    Task MarkAsReadAsync(Guid uid, CancellationToken cancellationToken);
    Task DismissAsync(Guid uid, CancellationToken cancellationToken);
}
