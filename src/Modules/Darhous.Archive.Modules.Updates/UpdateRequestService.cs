using System.Text.Json;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Modules.Updates.Jobs;
using Darhous.Archive.Modules.Updates.Persistence;

namespace Darhous.Archive.Modules.Updates;

internal sealed class UpdateRequestService(
    IUnitOfWork unitOfWork,
    UpdateHistoryStore history) : IUpdateRequestService
{
    public Task<Guid> QueueInstallAsync(
        string packagePath,
        Guid? initiatedBy,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        return QueueAsync(
            new UpdateJobPayload("install", Path.GetFullPath(packagePath), null, initiatedBy),
            initiatedBy,
            cancellationToken);
    }

    public async Task<Guid> QueueLatestRollbackAsync(Guid? initiatedBy, CancellationToken cancellationToken)
    {
        var latest = await history.GetLatestRollbackReadyAsync(cancellationToken)
            ?? throw new InvalidOperationException("No completed update has a rollback point.");
        return await QueueAsync(
            new UpdateJobPayload("rollback", null, latest.Id, initiatedBy),
            initiatedBy,
            cancellationToken);
    }

    private Task<Guid> QueueAsync(
        UpdateJobPayload payload,
        Guid? initiatedBy,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(
            (context, ct) => context.Jobs.CreateAsync(
                new NewJob(
                    nameof(UpdateJob),
                    "Darhous.Archive.Modules.Updates",
                    null,
                    initiatedBy,
                    JsonSerializer.Serialize(payload, UpdateJsonContext.Default.UpdateJobPayload),
                    MaxRetries: 0,
                    Priority: 100,
                    CorrelationId: null),
                ct),
            cancellationToken);
}
