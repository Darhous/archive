using Darhous.Archive.Core.Health;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Workers.Host;
using Darhous.Archive.Workers.Protocol;
using Darhous.Archive.Workers.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace Darhous.Archive.Modules.Ocr;

internal interface IManagedOcrProvider : IOcrProvider, IHostedService, IAsyncDisposable
{
}

/// <summary>Owns the Phase 13 supervised OCR process and its handshaken named-pipe session.</summary>
internal sealed class WorkerOcrProvider : IManagedOcrProvider
{
    private readonly OcrSupervisionOptions _options;
    private readonly IHealthRegistry _healthRegistry;
    private readonly IClock _clock;
    private readonly IWorkerDelayProvider _delayProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly ILogger<WorkerOcrProvider> _logger;
    private NamedPipeTransport? _transport;
    private WorkerProcessSupervisor? _supervisor;
    private OcrWorkerClient? _client;

    public WorkerOcrProvider(
        OcrSupervisionOptions options,
        IHealthRegistry healthRegistry,
        IClock clock,
        IWorkerDelayProvider delayProvider,
        ILoggerFactory loggerFactory)
    {
        _options = options;
        _healthRegistry = healthRegistry;
        _clock = clock;
        _delayProvider = delayProvider;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<WorkerOcrProvider>();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_client is not null) throw new InvalidOperationException("OCR provider is already started.");

        Directory.CreateDirectory(_options.Protocol.ManagedTempStorageRoot);
        var pipeName = NamedPipeTransport.CreateUniquePipeName(_options.Protocol);
        _transport = NamedPipeTransport.CreateServer(pipeName, _options.Protocol);
        var handshake = new WorkerHandshakeHost(_options.Protocol);

        var workerOptions = CopySupervisionOptions(
            _options.WorkerSupervision,
            pipeName,
            handshake.SessionToken,
            _options.Protocol.ManagedTempStorageRoot);
        _supervisor = new WorkerProcessSupervisor(
            _options.ExecutablePath,
            workerOptions,
            _delayProvider,
            _clock,
            _healthRegistry,
            _loggerFactory.CreateLogger<WorkerProcessSupervisor>());

        try
        {
            using var handshakeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            handshakeTimeout.CancelAfter(_options.Protocol.HandshakeTimeout);
            await _supervisor.StartAsync(cancellationToken);
            await _transport.WaitForConnectionAsync(handshakeTimeout.Token);
            await handshake.AcceptAsync(_transport, "Darhous.Archive.Ocr.Worker", handshakeTimeout.Token);
            _client = new OcrWorkerClient(_transport, _options.Protocol);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            await StopAsync(CancellationToken.None);
            throw new TimeoutException(
                $"OCR worker did not connect and handshake within {_options.Protocol.HandshakeTimeout}.",
                ex);
        }
        catch
        {
            await StopAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<OcrResult> ProcessAsync(
        string inputFilePath,
        IReadOnlyList<string>? languages = null,
        CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            if (_client is null) return OcrResult.Failed("worker_not_running", "OCR worker is not connected.");
            try
            {
                return await _client.ProcessAsync(inputFilePath, languages, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or WorkerProtocolException or ObjectDisposedException)
            {
                // The Phase 13 supervisor restarts a crashed process, but a named-pipe session and
                // handshake cannot be reused. Recreate the complete supervised session and retry once.
                _logger.LogWarning(ex, "OCR worker connection failed; recreating the supervised session");
                await StopAsync(CancellationToken.None);
                await StartAsync(cancellationToken);
                return await _client!.ProcessAsync(inputFilePath, languages, cancellationToken);
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _client = null;
        if (_transport is not null)
        {
            await _transport.DisposeAsync();
            _transport = null;
        }
        if (_supervisor is not null)
        {
            await _supervisor.StopAsync(cancellationToken);
            await _supervisor.DisposeAsync();
            _supervisor = null;
        }
    }

    private static WorkerSupervisionOptions CopySupervisionOptions(
        WorkerSupervisionOptions source,
        string pipeName,
        string sessionToken,
        string tempRoot)
    {
        var environment = new Dictionary<string, string>(source.EnvironmentVariables, StringComparer.OrdinalIgnoreCase)
        {
            ["DARHOUS_WORKER_PIPE_NAME"] = pipeName,
            ["DARHOUS_WORKER_SESSION_TOKEN"] = sessionToken,
            ["DARHOUS_WORKER_TEMP_ROOT"] = tempRoot,
        };

        return new WorkerSupervisionOptions
        {
            FirstRestartDelay = source.FirstRestartDelay,
            SecondRestartDelay = source.SecondRestartDelay,
            ThirdRestartDelay = source.ThirdRestartDelay,
            CrashLoopWindow = source.CrashLoopWindow,
            CrashLoopThreshold = source.CrashLoopThreshold,
            EnvironmentVariables = environment,
        };
    }

    public async ValueTask DisposeAsync() => await StopAsync(CancellationToken.None);
}
