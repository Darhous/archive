using System;
using System.Threading;
using System.Threading.Tasks;

namespace Darhous.Archive.Workers.Host;

public interface IWorkerDelayProvider
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class DefaultWorkerDelayProvider : IWorkerDelayProvider
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);
    }
}
