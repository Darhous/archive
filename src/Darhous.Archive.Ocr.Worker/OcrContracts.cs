using System.Text.Json.Serialization;
using Darhous.Archive.Workers.Protocol;

namespace Darhous.Archive.Ocr.Worker;

public static class OcrMessageTypes
{
    public const string Request = "ocr.request";
    public const string Result = "ocr.result";
    public const string ProtocolError = "protocol.error";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OcrRequest(LargeDataReference Input, IReadOnlyList<string> Languages);

public sealed record OcrSuccess(LargeDataReference SearchablePdf, string Text);

public sealed record OcrFailure(string Code, string Message);

public sealed record OcrEngineAvailability(bool IsAvailable, string? Detail = null);

public sealed record OcrWord(string Text, int X, int Y, int Width, int Height);

public sealed record OcrPageText(
    string Text,
    int ImageWidth,
    int ImageHeight,
    IReadOnlyList<OcrWord> Words,
    double? PdfLeft = null,
    double? PdfTop = null,
    double? PdfWidth = null,
    double? PdfHeight = null);

public sealed record OcrEngineResult(string? Text, string? SearchablePdfPath, OcrFailure? Failure)
{
    public static OcrEngineResult Failed(string code, string message) => new(null, null, new(code, message));
}

/// <summary>
/// OCR implementation boundary. Implementations read an original PDF and create a separate
/// searchable PDF at <paramref name="outputPdfPath"/>; they must never modify the input.
/// </summary>
public interface IOcrEngine : IDisposable
{
    OcrEngineAvailability Availability { get; }

    Task<OcrEngineResult> ProcessAsync(
        string inputPdfPath,
        string outputPdfPath,
        IReadOnlyList<string> languages,
        CancellationToken cancellationToken);
}
