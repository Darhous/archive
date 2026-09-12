using System.Text.Encodings.Web;
using System.Text.Json;
using MimeKit;

namespace Darhous.Archive.Modules.Importers.Extractors;

/// <summary>Implementation Plan §58 (MSG/EML) — Subject/From/To/CC/Date/Body/Attachments metadata.</summary>
public sealed class EmlContentExtractor : IContentExtractor
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public bool CanHandle(string fileExtension) => string.Equals(fileExtension, ".eml", StringComparison.OrdinalIgnoreCase);

    public async Task<ExtractedContent> ExtractAsync(string filePath, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(filePath);
        var message = await MimeMessage.LoadAsync(stream, cancellationToken);

        var body = message.TextBody ?? message.HtmlBody ?? "";
        var attachmentNames = message.Attachments.Select(a => a.ContentDisposition?.FileName ?? a.ContentType.Name ?? "attachment").ToList();

        var metadata = JsonSerializer.Serialize(new
        {
            message.Subject,
            From = message.From.ToString(),
            To = message.To.ToString(),
            Cc = message.Cc.ToString(),
            Date = message.Date.ToString("O"),
            Attachments = attachmentNames,
        }, MetadataJsonOptions);

        return new ExtractedContent(body.Length > 0 ? body : null, PageCount: null, IsSearchablePdf: null, metadata);
    }
}
