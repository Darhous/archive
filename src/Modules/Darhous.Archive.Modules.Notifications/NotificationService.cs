using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Darhous.Archive.Core.Time;

namespace Darhous.Archive.Modules.Notifications;

public sealed class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;
    private readonly IClock _clock;
    private readonly IEnumerable<INotificationProvider> _providers;

    public NotificationService(INotificationRepository repository, IClock clock, IEnumerable<INotificationProvider> providers)
    {
        _repository = repository;
        _clock = clock;
        _providers = providers;
    }

    public async Task<Notification> CreateAsync(string type, string severity, string title, string body, string source, string? actionJson, CancellationToken cancellationToken)
    {
        var notification = new Notification(
            Id: 0,
            Uid: Guid.CreateVersion7(),
            UserId: null,
            Type: type,
            Severity: severity,
            Title: title,
            Body: body,
            Source: source,
            ActionJson: actionJson,
            CreatedAt: _clock.UtcNow,
            ReadAt: null,
            DismissedAt: null
        );

        var saved = await _repository.CreateAsync(notification, cancellationToken);

        foreach (var provider in _providers)
        {
            try
            {
                await provider.ShowAsync(saved, cancellationToken);
            }
            catch
            {
                // Ignore provider failures
            }
        }

        return saved;
    }

    public Task<IReadOnlyList<Notification>> ListAsync(bool unreadOnly, int limit, int offset, CancellationToken cancellationToken)
    {
        return _repository.ListAsync(unreadOnly, limit, offset, cancellationToken);
    }

    public Task MarkAsReadAsync(Guid uid, CancellationToken cancellationToken)
    {
        return _repository.MarkAsReadAsync(uid, _clock.UtcNow, cancellationToken);
    }

    public Task DismissAsync(Guid uid, CancellationToken cancellationToken)
    {
        return _repository.DismissAsync(uid, _clock.UtcNow, cancellationToken);
    }
}
