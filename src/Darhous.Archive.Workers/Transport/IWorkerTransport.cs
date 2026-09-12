using Darhous.Archive.Workers.Protocol;

namespace Darhous.Archive.Workers.Transport;

public interface IWorkerTransport : IAsyncDisposable
{
    ValueTask SendAsync(WorkerMessage message, CancellationToken cancellationToken = default);

    ValueTask<WorkerMessage> ReceiveAsync(CancellationToken cancellationToken = default);
}
