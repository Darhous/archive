using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Modules.Ai;

public sealed class AiWorkerOptions
{
    public int QueueCapacity { get; init; } = 32;

    public TimeSpan ProviderTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

public interface IAiWorker
{
    Task<IReadOnlyList<AiModelInfo>> ListModelsAsync(
        IAiProvider provider,
        CancellationToken cancellationToken);

    Task<AiResponse> ExecuteAsync(
        IAiProvider provider,
        AiRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// A real in-process worker boundary for Phase 20. Calls enter a bounded channel and provider
/// code is invoked through Task.Run by a hosted consumer, so synchronous provider startup can
/// never execute on the WPF thread. Each call has a hard wait timeout even when a provider
/// ignores cancellation. Out-of-process IPC is deferred until a concrete provider protocol
/// exists; this boundary can then be replaced without changing IAiProvider or AiAssistService.
/// </summary>
public sealed class AiWorker : BackgroundService, IAiWorker
{
    private readonly Channel<IWorkItem> _channel;
    private readonly AiWorkerOptions _options;
    private readonly ILogger<AiWorker> _logger;

    public AiWorker(AiWorkerOptions options, ILogger<AiWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.QueueCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "AI worker queue capacity must be positive.");
        }

        if (options.ProviderTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "AI provider timeout must be positive.");
        }

        _options = options;
        _logger = logger;
        _channel = Channel.CreateBounded<IWorkItem>(new BoundedChannelOptions(options.QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    public Task<IReadOnlyList<AiModelInfo>> ListModelsAsync(
        IAiProvider provider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return EnqueueAsync(provider.ListModelsAsync, cancellationToken);
    }

    public Task<AiResponse> ExecuteAsync(
        IAiProvider provider,
        AiRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(request);
        return EnqueueAsync(ct => provider.ExecuteAsync(request, ct), cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var workItem in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await workItem.RunAsync(stoppingToken);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Unhandled failure escaped an AI worker item");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal hosted-service shutdown.
        }
        finally
        {
            while (_channel.Reader.TryRead(out var pending))
            {
                pending.Cancel(stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }

    private async Task<T> EnqueueAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        var item = new WorkItem<T>(operation, _options.ProviderTimeout, cancellationToken);
        if (!await _channel.Writer.WaitToWriteAsync(cancellationToken))
        {
            throw new InvalidOperationException("The AI worker is stopped.");
        }

        await _channel.Writer.WriteAsync(item, cancellationToken);
        return await item.Completion;
    }

    private interface IWorkItem
    {
        Task RunAsync(CancellationToken stoppingToken);

        void Cancel(CancellationToken cancellationToken);
    }

    private sealed class WorkItem<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        CancellationToken callerToken) : IWorkItem
    {
        private readonly TaskCompletionSource<T> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<T> Completion => _completion.Task;

        public void Cancel(CancellationToken cancellationToken) =>
            _completion.TrySetCanceled(cancellationToken);

        public async Task RunAsync(CancellationToken stoppingToken)
        {
            if (callerToken.IsCancellationRequested)
            {
                _completion.TrySetCanceled(callerToken);
                return;
            }

            using var invocationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                callerToken,
                stoppingToken);
            var providerTask = Task.Run(
                () => operation(invocationCancellation.Token),
                CancellationToken.None);

            try
            {
                var result = await providerTask.WaitAsync(timeout, stoppingToken);
                _completion.TrySetResult(result);
            }
            catch (TimeoutException exception)
            {
                invocationCancellation.Cancel();
                ObserveLateCompletion(providerTask);
                _completion.TrySetException(new TimeoutException(
                    $"AI provider call exceeded the configured timeout of {timeout}.",
                    exception));
            }
            catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
            {
                _completion.TrySetCanceled(callerToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _completion.TrySetCanceled(stoppingToken);
            }
            catch (Exception exception)
            {
                _completion.TrySetException(exception);
            }
        }

        private static void ObserveLateCompletion(Task providerTask) =>
            _ = providerTask.ContinueWith(
                static task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }
}
