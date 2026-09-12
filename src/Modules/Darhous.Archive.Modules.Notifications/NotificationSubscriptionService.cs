using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Darhous.Archive.Contracts.Events;
using Darhous.Archive.Core.Events;
using Microsoft.Extensions.Hosting;

namespace Darhous.Archive.Modules.Notifications;

public sealed class NotificationSubscriptionService : BackgroundService
{
    private readonly IEventBus _eventBus;
    private readonly INotificationService _notificationService;

    public NotificationSubscriptionService(IEventBus eventBus, INotificationService notificationService)
    {
        _eventBus = eventBus;
        _notificationService = notificationService;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _eventBus.Subscribe<JobFailedEvent>(async (envelope, ct) =>
        {
            var payload = envelope.Payload;
            var body = $"Job {payload.JobType} ({payload.JobUid}) failed: {payload.ErrorMessage}";
            var actionJson = JsonSerializer.Serialize(new { JobUid = payload.JobUid });

            await _notificationService.CreateAsync(
                type: "job.failed",
                severity: payload.Severity,
                title: "Job Failed",
                body: body,
                source: "jobs",
                actionJson: actionJson,
                cancellationToken: ct
            );
        });

        return Task.CompletedTask;
    }
}
