using System;
using System.Threading;
using System.Threading.Tasks;
using Darhous.Archive.PluginSdk;
using Darhous.Archive.PluginSdk.Configuration;
using Darhous.Archive.PluginSdk.Runtime;
using Darhous.Archive.Workers;
using Darhous.Archive.Workers.Host;
using Darhous.Archive.Workers.Protocol;
using Darhous.Archive.Workers.Transport;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Core.Health;
using Darhous.Archive.Modules.Scanner.Persistence;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace Darhous.Archive.Modules.Scanner;

[SupportedOSPlatform("windows")]
public sealed class ScannerPlugin : IArchivePlugin
{
    private readonly ScannerSupervisionOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<ScannerPlugin> _logger;
    private readonly ILogger<WorkerProcessSupervisor> _supervisorLogger;
    
    private WorkerProcessSupervisor? _supervisor;
    private ScannerProvider? _provider;
    private WorkerSupervisionOptions? _supervisionOptions;

    public ScannerPlugin(
        ScannerSupervisionOptions options, 
        IClock clock, 
        ILogger<ScannerPlugin> logger,
        ILogger<WorkerProcessSupervisor> supervisorLogger)
    {
        _options = options;
        _clock = clock;
        _logger = logger;
        _supervisorLogger = supervisorLogger;
    }

    public PluginIdentity Identity => new PluginIdentity("darhous.scanner", new Version(1, 0, 0), "Darhous", "Scanner Plugin");

    public ValueTask ConfigureAsync(IPluginConfigurationContext context, CancellationToken cancellationToken)
    {
        context.Services.AddSingleton<IScannerProfileRepository, ScannerProfileRepository>();

        _supervisionOptions = new WorkerSupervisionOptions
        {
            CrashLoopWindow = _options.CrashLoopWindow,
            CrashLoopThreshold = _options.CrashLoopThreshold,
            FirstRestartDelay = _options.FirstRestartDelay,
            SecondRestartDelay = _options.SecondRestartDelay,
            ThirdRestartDelay = _options.ThirdRestartDelay,
        };
        foreach(var kv in _options.EnvironmentVariables) _supervisionOptions.EnvironmentVariables[kv.Key] = kv.Value;

        _supervisor = new WorkerProcessSupervisor(
            _options.ExecutablePath,
            _supervisionOptions,
            new DefaultWorkerDelayProvider(),
            _clock,
            context.Health,
            _supervisorLogger
        );

        _provider = new ScannerProvider(_supervisor);
        context.Services.AddSingleton<IScannerProvider>(_provider);
        context.Services.AddSingleton<WorkerProcessSupervisor>(_supervisor);
        
        return ValueTask.CompletedTask;
    }

    public async ValueTask StartAsync(IPluginRuntimeContext context, CancellationToken cancellationToken)
    {
        if (_supervisor == null || _provider == null || _supervisionOptions == null)
            throw new InvalidOperationException("ConfigureAsync must be called first.");

        var protocolOptions = new WorkerProtocolOptions();
        var pipeName = NamedPipeTransport.CreateUniquePipeName(protocolOptions);
        var handshakeHost = new WorkerHandshakeHost(protocolOptions);
        
        _supervisionOptions.EnvironmentVariables["SCANNER_PIPE_NAME"] = pipeName;
        _supervisionOptions.EnvironmentVariables["SCANNER_SESSION_TOKEN"] = handshakeHost.SessionToken;

        await _supervisor.StartAsync(cancellationToken);

        var server = NamedPipeTransport.CreateServer(pipeName, protocolOptions);
        
        // Fire-and-forget background connection wait so StartAsync doesn't block host startup.
        _ = Task.Run(async () =>
        {
            try
            {
                await server.WaitForConnectionAsync(CancellationToken.None);
                await handshakeHost.AcceptAsync(server, null, CancellationToken.None);
                _provider.Initialize(server);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to worker and handshake");
            }
        });
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        if (_supervisor != null)
        {
            await _supervisor.StopAsync(cancellationToken);
            await _supervisor.DisposeAsync();
        }
    }
}
