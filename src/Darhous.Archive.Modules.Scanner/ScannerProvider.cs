using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Darhous.Archive.Workers;
using Darhous.Archive.Workers.Protocol;
using Darhous.Archive.Workers.Transport;
using Darhous.Archive.Workers.Host;

namespace Darhous.Archive.Modules.Scanner;

public sealed class ScannerProvider : IScannerProvider
{
    private IWorkerTransport? _transport;
    private readonly WorkerProcessSupervisor _supervisor;

    public ScannerProvider(WorkerProcessSupervisor supervisor)
    {
        _supervisor = supervisor;
    }

    public void Initialize(IWorkerTransport transport)
    {
        _transport = transport;
    }

    public Task<ScannerHealthStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(new ScannerHealthStatus(_supervisor.CurrentStatus, ""));
    }

    public async Task<ScanResult> ScanAsync(ScannerProfile profile, CancellationToken cancellationToken)
    {
        if (_transport == null)
            throw new InvalidOperationException("Scanner provider not initialized with a transport.");

        var request = new ScanProfileRequest
        {
            Resolution = profile.Resolution,
            ColorMode = Enum.TryParse<ScanColorMode>(profile.ColorMode, out var cMode) ? cMode : ScanColorMode.Color,
            Duplex = profile.Duplex,
            Source = Enum.TryParse<ScanSource>(profile.Source, out var sMode) ? sMode : ScanSource.Adf,
            Separation = profile.PageSeparationMode switch
            {
                "Every Page" => PageSeparation.EveryPage,
                "Every 2 Pages" => PageSeparation.EveryTwoPages,
                "Every N Pages" => PageSeparation.EveryNPages,
                _ => PageSeparation.FullBatch
            }
        };

        var protocolOptions = new WorkerProtocolOptions();
        var message = WorkerMessage.Create(protocolOptions.ProtocolVersion, "scan.request", request);
        await _transport.SendAsync(message, cancellationToken);
        var response = await _transport.ReceiveAsync(cancellationToken);
        
        if (response.MessageType == "scan.result")
        {
            if (response.Payload.TryGetProperty("Path", out _))
            {
                var success = response.DeserializePayload<LargeDataReference>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new ScanResult(success!.Path, true, null);
            }
            else
            {
                var failure = response.DeserializePayload<ScanFailure>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return new ScanResult(string.Empty, false, $"[{failure!.Code}] {failure.Message}");
            }
        }
        
        throw new InvalidOperationException($"Unexpected message type: {response.MessageType}");
    }
}
