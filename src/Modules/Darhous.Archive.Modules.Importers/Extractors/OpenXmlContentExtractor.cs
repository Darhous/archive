using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Drawing = DocumentFormat.OpenXml.Drawing;

namespace Darhous.Archive.Modules.Importers.Extractors;

/// <summary>Implementation Plan §56 (DOCX/XLSX/PPTX via Open XML SDK).</summary>
public sealed class OpenXmlContentExtractor : IContentExtractor
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".docx", ".xlsx", ".pptx" };

    public bool CanHandle(string fileExtension) => SupportedExtensions.Contains(fileExtension);

    public Task<ExtractedContent> ExtractAsync(string filePath, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(filePath);

        var content = extension.ToLowerInvariant() switch
        {
            ".docx" => ExtractDocx(filePath),
            ".xlsx" => ExtractXlsx(filePath, cancellationToken),
            ".pptx" => ExtractPptx(filePath, cancellationToken),
            _ => throw new NotSupportedException($"Unsupported extension for OpenXmlContentExtractor: {extension}"),
        };

        return Task.FromResult(content);
    }

    private static ExtractedContent ExtractDocx(string filePath)
    {
        using var document = WordprocessingDocument.Open(filePath, false);
        var text = document.MainDocumentPart?.Document?.Body?.InnerText ?? "";

        // Word page count isn't derivable from the XML without a layout pass — left null rather
        // than fabricated (DB Spec's page_count is genuinely nullable for this reason).
        return new ExtractedContent(text.Length > 0 ? text : null, PageCount: null, IsSearchablePdf: null, MetadataJson: null);
    }

    private static ExtractedContent ExtractPptx(string filePath, CancellationToken cancellationToken)
    {
        using var document = PresentationDocument.Open(filePath, false);
        var slideParts = document.PresentationPart?.SlideParts.ToList() ?? [];

        var builder = new StringBuilder();
        foreach (var slidePart in slideParts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var textElement in slidePart.Slide?.Descendants<Drawing.Text>() ?? [])
            {
                builder.Append(textElement.Text).Append(' ');
            }

            builder.Append('\n');
        }

        var text = builder.ToString();
        return new ExtractedContent(text.Trim().Length > 0 ? text : null, slideParts.Count, IsSearchablePdf: null, MetadataJson: null);
    }

    private static ExtractedContent ExtractXlsx(string filePath, CancellationToken cancellationToken)
    {
        using var document = SpreadsheetDocument.Open(filePath, false);
        var workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("XLSX has no workbook part.");
        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;

        var builder = new StringBuilder();
        var sheetCount = 0;

        foreach (var sheet in workbookPart.Workbook?.Descendants<Sheet>() ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sheet.Id?.Value is not { } relationshipId || workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart)
            {
                continue;
            }

            sheetCount++;
            foreach (var cell in worksheetPart.Worksheet?.Descendants<Cell>() ?? [])
            {
                var value = ReadCellText(cell, sharedStrings);
                if (!string.IsNullOrEmpty(value))
                {
                    builder.Append(value).Append(' ');
                }
            }

            builder.Append('\n');
        }

        var text = builder.ToString();
        return new ExtractedContent(text.Trim().Length > 0 ? text : null, sheetCount, IsSearchablePdf: null, MetadataJson: null);
    }

    private static string? ReadCellText(Cell cell, SharedStringTable? sharedStrings)
    {
        var rawValue = cell.CellValue?.Text;
        if (rawValue is null)
        {
            return cell.InlineString?.Text?.Text;
        }

        if (cell.DataType?.Value == CellValues.SharedString && sharedStrings is not null && int.TryParse(rawValue, out var index))
        {
            return sharedStrings.ElementAtOrDefault(index)?.InnerText;
        }

        return rawValue;
    }
}
