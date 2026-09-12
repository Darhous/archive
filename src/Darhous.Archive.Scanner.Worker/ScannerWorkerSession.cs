using System.IO.Compression;
using System.Text.Json;
using Darhous.Archive.Workers;
using Darhous.Archive.Workers.Protocol;
using Darhous.Archive.Workers.Transport;

namespace Darhous.Archive.Scanner.Worker;

public sealed class ScannerWorkerSession(IScannerEngine engine, WorkerProtocolOptions options)
{
    public const string WorkerId = "Darhous.Archive.Scanner.Worker";
    public static IReadOnlyList<string> Capabilities { get; } = Array.AsReadOnly(new[]
    {
        "scan.adf", "scan.flatbed", "scan.duplex", "scan.single", "scan.color",
        "scan.grayscale", "scan.blackwhite", "scan.page-separation",
    });

    public async Task RunAsync(IWorkerTransport transport, string token, CancellationToken cancellationToken)
    {
        IReadOnlyList<ScannerDevice> devices;
        try { devices = await engine.GetDevicesAsync(cancellationToken); }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { devices = []; }
        await new WorkerHandshakeClient(options).PerformAsync(transport,
            new(WorkerId, "1.0.0", Capabilities, devices.Count > 0
                ? new("healthy") : new("degraded", "No scanner available; connect a scanner and install its driver.")),
            token, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            WorkerMessage request;
            try { request = await transport.ReceiveAsync(cancellationToken); }
            catch (WorkerProtocolException) { return; } // Invalid framing/EOF cannot be recovered safely.
            catch (IOException) { return; }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }

            WorkerMessage response;
            if (request.ProtocolVersion != options.ProtocolVersion || request.MessageType != ScannerMessageTypes.Request)
            {
                response = Reply(request, ScannerMessageTypes.ProtocolError,
                    new ScanFailure("unsupported_message", "Expected scan.request with protocol version 1."));
            }
            else
            {
                response = await HandleScanAsync(request, cancellationToken);
            }
            try { await transport.SendAsync(response, cancellationToken); }
            catch (IOException) { return; }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
        }
    }

    private async Task<WorkerMessage> HandleScanAsync(WorkerMessage request, CancellationToken cancellationToken)
    {
        string? directory = null;
        bool success = false;
        try
        {
            var profile = request.DeserializePayload<ScanProfile>() ?? throw new ArgumentException("Profile is required.");
            profile.Validate();
            var validator = new ManagedTempPathValidator(options);
            directory = validator.ValidateAndNormalize(Path.Combine(options.ManagedTempStorageRoot, Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            var result = await engine.ScanAsync(profile, directory, cancellationToken);
            if (result.Failure is not null) return Reply(request, ScannerMessageTypes.Result, result.Failure);
            if (result.Files.Count == 0) return Reply(request, ScannerMessageTypes.Result, new ScanFailure("no_pages", "The scanner returned no pages."));
            var batchValidator = new ManagedTempPathValidator(new() { ManagedTempStorageRoot = directory });
            var files = result.Files.Select(batchValidator.ValidateAndNormalize).ToArray();
            foreach (var file in files)
            {
                if (!File.Exists(file) || new FileInfo(file).Length == 0)
                    throw new IOException("Scanner output is missing or empty.");
            }

            // A single reference per request: a PDF, or an ordered ZIP of separated PDFs.
            var output = files[0];
            if (files.Length > 1)
            {
                output = Path.Combine(directory, "batch.zip");
                using var archive = ZipFile.Open(output, ZipArchiveMode.Create);
                for (var i = 0; i < files.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    archive.CreateEntryFromFile(files[i], $"document-{i + 1:D4}.pdf");
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            success = true;
            return Reply(request, ScannerMessageTypes.Result, new LargeDataReference(output));
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return Reply(request, ScannerMessageTypes.Result, new ScanFailure("invalid_profile", "The scan profile is invalid."));
        }
        catch (OperationCanceledException)
        {
            return Reply(request, ScannerMessageTypes.Result, new ScanFailure("cancelled", "Scan cancelled."));
        }
        catch (Exception)
        {
            return Reply(request, ScannerMessageTypes.Result, new ScanFailure("device_error", "Scan or output storage failed."));
        }
        finally
        {
            if (!success && directory is not null)
            {
                try { Directory.Delete(directory, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private WorkerMessage Reply<T>(WorkerMessage request, string type, T payload) =>
        WorkerMessage.Create(options.ProtocolVersion, type, payload, correlationId: request.RequestId);
}
