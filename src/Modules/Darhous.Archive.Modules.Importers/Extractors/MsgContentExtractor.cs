using System.Text.Encodings.Web;
using System.Text.Json;
using MsgReader.Outlook;

namespace Darhous.Archive.Modules.Importers.Extractors;

/// <summary>Implementation Plan §58 (MSG/EML) — Outlook's binary format, via MsgReader (the standard library for this since Outlook itself isn't a dependency here).</summary>
public sealed class MsgContentExtractor : IContentExtractor
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public bool CanHandle(string fileExtension) => string.Equals(fileExtension, ".msg", StringComparison.OrdinalIgnoreCase);

    public Task<ExtractedContent> ExtractAsync(string filePath, CancellationToken cancellationToken)
    {
        using var message = new Storage.Message(filePath);

        var to = message.GetEmailRecipients(RecipientType.To, false, false);
        var cc = message.GetEmailRecipients(RecipientType.Cc, false, false);
        var body = message.BodyText ?? "";
        var attachmentNames = message.Attachments
            .OfType<Storage.Attachment>()
            .Select(a => a.FileName)
            .ToList();

        var metadata = JsonSerializer.Serialize(new
        {
            message.Subject,
            From = message.Sender?.Email,
            To = to,
            Cc = cc,
            Date = message.SentOn?.ToString("O"),
            Attachments = attachmentNames,
        }, MetadataJsonOptions);

        return Task.FromResult(new ExtractedContent(body.Length > 0 ? body : null, PageCount: null, IsSearchablePdf: null, metadata));
    }
}
