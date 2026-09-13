using System.Text.Json;
using Darhous.Archive.Application.Persistence;
using Darhous.Backup.Local.Jobs;

namespace Darhous.Backup.Local;

internal sealed class BackupRequestService(IUnitOfWork unitOfWork) : IBackupRequestService
{
    public Task<Guid> QueueAsync(BackupRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DestinationDirectory);
        var payload = new BackupJobPayload(request.Type, Path.GetFullPath(request.DestinationDirectory), request.RequestedBy);
        return unitOfWork.ExecuteAsync(
            (context, ct) => context.Jobs.CreateAsync(
                new NewJob(
                    nameof(BackupJob),
                    "Darhous.Backup.Local",
                    "Darhous.Backup.Local",
                    request.RequestedBy,
                    JsonSerializer.Serialize(payload),
                    MaxRetries: 1,
                    Priority: 0,
                    CorrelationId: null),
                ct),
            cancellationToken);
    }
}
