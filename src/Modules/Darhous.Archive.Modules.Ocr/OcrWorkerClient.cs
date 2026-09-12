using System.Text.Json;
using Darhous.Archive.Workers;
using Darhous.Archive.Workers.Protocol;
using Darhous.Archive.Workers.Transport;

namespace Darhous.Archive.Modules.Ocr;

internal static class OcrWireMessageTypes
{
    public const string Request = "ocr.request";
    public const string Result = "ocr.result";
    public const string ProtocolError = "protocol.error";
}

internal sealed record OcrWorkerRequest(LargeDataReference Input, IReadOnlyList<string> Languages);
internal sealed record OcrWorkerSuccess(LargeDataReference SearchablePdf, string Text);
internal sealed record OcrWorkerFailure(string Code, string Message);

/// <summary>Sequential host-side request client over an already handshaken transport.</summary>
internal sealed class OcrWorkerClient(IWorkerTransport transport, WorkerProtocolOptions options) : IOcrProvider
{
    private readonly SemaphoreSlim _requestLock = new(1, 1);

    public async Task<OcrResult> ProcessAsync(
        string inputFilePath,
        IReadOnlyList<string>? languages = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputFilePath);
        var selectedLanguages = languages is null || languages.Count == 0 ? new[] { "ara" } : languages.ToArray();
        ValidateLanguages(selectedLanguages);

        await _requestLock.WaitAsync(cancellationToken);
        string? requestDirectory = null;
        try
        {
            var validator = new ManagedTempPathValidator(options);
            requestDirectory = validator.ValidateAndNormalize(Path.Combine(
                options.ManagedTempStorageRoot,
                $"ocr-input-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(requestDirectory);
            var managedInput = validator.ValidateAndNormalize(Path.Combine(requestDirectory, "input.pdf"));
            File.Copy(inputFilePath, managedInput, overwrite: false);

            var requestId = Guid.NewGuid().ToString("N");
            await transport.SendAsync(
                WorkerMessage.Create(
                    options.ProtocolVersion,
                    OcrWireMessageTypes.Request,
                    new OcrWorkerRequest(new LargeDataReference(managedInput), selectedLanguages),
                    requestId: requestId),
                cancellationToken);

            var response = await transport.ReceiveAsync(cancellationToken);
            if (response.ProtocolVersion != options.ProtocolVersion ||
                response.MessageType != OcrWireMessageTypes.Result ||
                response.CorrelationId != requestId)
            {
                throw new WorkerProtocolException("OCR worker returned an unexpected message envelope.");
            }

            if (response.Payload.TryGetProperty(nameof(OcrWorkerFailure.Code), out _))
            {
                var failure = response.DeserializePayload<OcrWorkerFailure>()
                    ?? throw new WorkerProtocolException("OCR worker returned a null failure payload.");
                return OcrResult.Failed(failure.Code, failure.Message);
            }

            OcrWorkerSuccess success;
            try
            {
                success = response.DeserializePayload<OcrWorkerSuccess>()
                    ?? throw new WorkerProtocolException("OCR worker returned a null success payload.");
            }
            catch (JsonException ex)
            {
                throw new WorkerProtocolException("OCR worker returned an invalid success payload.", ex);
            }

            var outputPath = validator.ValidateAndNormalize(success.SearchablePdf.Path);
            if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            {
                throw new WorkerProtocolException("OCR worker returned a missing or empty searchable PDF.");
            }

            return OcrResult.Succeeded(success.Text, outputPath);
        }
        finally
        {
            if (requestDirectory is not null)
            {
                try { Directory.Delete(requestDirectory, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            _requestLock.Release();
        }
    }

    private static void ValidateLanguages(IReadOnlyList<string> languages)
    {
        if (languages.Count > 2 || languages.Any(language => language is not "ara" and not "eng") ||
            languages.Distinct(StringComparer.Ordinal).Count() != languages.Count)
        {
            throw new ArgumentException("Languages must contain ara, eng, or both without duplicates.", nameof(languages));
        }
    }
}
