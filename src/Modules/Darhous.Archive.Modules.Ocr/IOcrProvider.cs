namespace Darhous.Archive.Modules.Ocr;

/// <summary>Host-facing OCR contract from master documentation §41.3.</summary>
public interface IOcrProvider
{
    /// <summary>
    /// Creates a derived searchable PDF in managed temporary storage and returns its plain text.
    /// A null or empty language list selects Arabic (<c>ara</c>) by default.
    /// The caller owns successful temporary output and must copy or remove it.
    /// </summary>
    Task<OcrResult> ProcessAsync(
        string inputFilePath,
        IReadOnlyList<string>? languages = null,
        CancellationToken cancellationToken = default);
}

public sealed record OcrResult(
    bool IsSuccess,
    string? ExtractedText,
    string? SearchableFilePath,
    string? FailureCode,
    string? FailureMessage)
{
    public static OcrResult Succeeded(string extractedText, string searchableFilePath) =>
        new(true, extractedText, searchableFilePath, null, null);

    public static OcrResult Failed(string code, string message) =>
        new(false, null, null, code, message);
}
