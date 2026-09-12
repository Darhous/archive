using System.Text.Json;
using Darhous.Archive.Workers;
using Darhous.Archive.Workers.Protocol;
using Darhous.Archive.Workers.Transport;

namespace Darhous.Archive.Ocr.Worker;

public sealed class OcrWorkerSession(IOcrEngine engine, WorkerProtocolOptions options)
{
    public const string WorkerId = "Darhous.Archive.Ocr.Worker";
    public static IReadOnlyList<string> Capabilities { get; } = Array.AsReadOnly(["ocr.pdf", "ocr.ara", "ocr.eng"]);

    public async Task RunAsync(IWorkerTransport transport, string token, CancellationToken cancellationToken)
    {
        var health = engine.Availability.IsAvailable
            ? new WorkerHealthPayload("healthy")
            : new WorkerHealthPayload("degraded", engine.Availability.Detail ?? "OCR engine unavailable.");

        await new WorkerHandshakeClient(options).PerformAsync(
            transport,
            new WorkerHandshakeDescription(WorkerId, "1.0.0", Capabilities, health),
            token,
            cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            WorkerMessage request;
            try
            {
                request = await transport.ReceiveAsync(cancellationToken);
            }
            catch (WorkerProtocolException)
            {
                return;
            }
            catch (IOException)
            {
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            WorkerMessage response;
            if (request.ProtocolVersion != options.ProtocolVersion || request.MessageType != OcrMessageTypes.Request)
            {
                response = Reply(request, OcrMessageTypes.ProtocolError,
                    new OcrFailure("unsupported_message", $"Expected {OcrMessageTypes.Request} with protocol version {options.ProtocolVersion}."));
            }
            else
            {
                response = await HandleAsync(request, cancellationToken);
            }

            try
            {
                await transport.SendAsync(response, cancellationToken);
            }
            catch (IOException)
            {
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task<WorkerMessage> HandleAsync(WorkerMessage request, CancellationToken cancellationToken)
    {
        string? outputPath = null;
        try
        {
            var payload = request.DeserializePayload<OcrRequest>() ?? throw new ArgumentException("OCR request is required.");
            ValidateLanguages(payload.Languages);

            var validator = new ManagedTempPathValidator(options);
            var inputPath = validator.ValidateAndNormalize(payload.Input.Path);
            if (!File.Exists(inputPath) || !string.Equals(Path.GetExtension(inputPath), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return Reply(request, OcrMessageTypes.Result, new OcrFailure("invalid_input", "Input must be an existing PDF in managed temporary storage."));
            }

            var outputDirectory = validator.ValidateAndNormalize(Path.Combine(
                options.ManagedTempStorageRoot,
                $"ocr-output-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(outputDirectory);
            outputPath = validator.ValidateAndNormalize(Path.Combine(outputDirectory, "searchable.pdf"));

            var result = await engine.ProcessAsync(inputPath, outputPath, payload.Languages, cancellationToken);
            if (result.Failure is not null)
            {
                DeleteOutput(outputDirectory);
                return Reply(request, OcrMessageTypes.Result, result.Failure);
            }

            var normalizedOutput = validator.ValidateAndNormalize(result.SearchablePdfPath ?? outputPath);
            if (!File.Exists(normalizedOutput) || new FileInfo(normalizedOutput).Length == 0 || result.Text is null)
            {
                DeleteOutput(outputDirectory);
                return Reply(request, OcrMessageTypes.Result, new OcrFailure("invalid_output", "OCR output is missing or incomplete."));
            }

            return Reply(request, OcrMessageTypes.Result,
                new OcrSuccess(new LargeDataReference(normalizedOutput), result.Text));
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or WorkerProtocolException)
        {
            if (outputPath is not null) DeleteOutput(Path.GetDirectoryName(outputPath)!);
            return Reply(request, OcrMessageTypes.Result, new OcrFailure("invalid_request", "OCR request payload or managed path is invalid."));
        }
        catch (OperationCanceledException)
        {
            if (outputPath is not null) DeleteOutput(Path.GetDirectoryName(outputPath)!);
            return Reply(request, OcrMessageTypes.Result, new OcrFailure("cancelled", "OCR was cancelled."));
        }
        catch (Exception)
        {
            if (outputPath is not null) DeleteOutput(Path.GetDirectoryName(outputPath)!);
            return Reply(request, OcrMessageTypes.Result, new OcrFailure("ocr_failed", "OCR processing or output storage failed."));
        }
    }

    private static void ValidateLanguages(IReadOnlyList<string>? languages)
    {
        if (languages is null || languages.Count == 0 || languages.Count > 2 ||
            languages.Any(language => language is not "ara" and not "eng") ||
            languages.Distinct(StringComparer.Ordinal).Count() != languages.Count)
        {
            throw new ArgumentException("Languages must contain ara, eng, or both without duplicates.");
        }
    }

    private static void DeleteOutput(string directory)
    {
        try
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private WorkerMessage Reply<TPayload>(WorkerMessage request, string messageType, TPayload payload) =>
        WorkerMessage.Create(options.ProtocolVersion, messageType, payload, correlationId: request.RequestId);
}
