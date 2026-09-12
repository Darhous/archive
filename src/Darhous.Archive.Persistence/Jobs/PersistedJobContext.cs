using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Jobs;
using Darhous.Archive.Core.Jobs;

namespace Darhous.Archive.Persistence.Jobs;

internal sealed class PersistedJobContext(JobMetadata metadata, IUnitOfWork unitOfWork) : IJobContext
{
    public JobMetadata Metadata { get; } = metadata;

    public Task ReportProgressAsync(int percent, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.Jobs.ReportProgressAsync(Metadata.JobId, percent, ct);
            return null;
        }, cancellationToken);
}
