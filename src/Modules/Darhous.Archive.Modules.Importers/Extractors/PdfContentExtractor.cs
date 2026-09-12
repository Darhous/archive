using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using UglyToad.PdfPig;

namespace Darhous.Archive.Modules.Importers.Extractors;

/// <summary>
/// Implementation Plan §55 / OCR Decision Flow (§42): detects whether a PDF has a real text
/// layer or is image-only (scanned). A short/empty extracted text after visiting every page
/// means "needs OCR" — actually running OCR is Phase 15, not this one; this extractor only
/// makes the correct decision and stops there.
/// </summary>
public sealed class PdfContentExtractor : IContentExtractor
{
    private const int MinimumCharsForTextLayer = 20;
    private static readonly JsonSerializerOptions MetadataJsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public bool CanHandle(string fileExtension) => string.Equals(fileExtension, ".pdf", StringComparison.OrdinalIgnoreCase);

    public Task<ExtractedContent> ExtractAsync(string filePath, CancellationToken cancellationToken)
    {
        using var document = PdfDocument.Open(filePath);

        var builder = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.Append(page.Text).Append('\n');
        }

        var text = builder.ToString();
        var hasTextLayer = text.Replace("\n", "").Replace(" ", "").Length >= MinimumCharsForTextLayer;

        var metadata = JsonSerializer.Serialize(new { document.Information.Title, document.Information.Author }, MetadataJsonOptions);

        return Task.FromResult(new ExtractedContent(
            hasTextLayer ? text : null, document.NumberOfPages, hasTextLayer, metadata));
    }
}
