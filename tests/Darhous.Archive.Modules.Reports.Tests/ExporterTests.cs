using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Darhous.Archive.Modules.Reports.Exporting;
using PdfSharp.Pdf.IO;

namespace Darhous.Archive.Modules.Reports.Tests;

public sealed class ExporterTests
{
    [Fact]
    public async Task CsvExporter_WritesUtf8BomEscapesValuesAndPreventsFormulaExecution()
    {
        var report = new ReportDefinition(
            "Documents",
            ["Name", "Notes"],
            new string?[][]
            {
                ["وثيقة", "comma, quote \" and\nnewline"],
                ["=SUM(A1:A2)", null],
            });
        using var stream = new MemoryStream();

        await new CsvExporter().ExportAsync(report, stream, CancellationToken.None);

        var bytes = stream.ToArray();
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        var csv = Encoding.UTF8.GetString(bytes);
        Assert.Contains("وثيقة", csv);
        Assert.Contains("\"comma, quote \"\" and\nnewline\"", csv);
        Assert.Contains("'=SUM(A1:A2)", csv);
    }

    [Fact]
    public async Task ExcelExporter_WritesValidWorkbookWithHeadersRowsAndMetadata()
    {
        var report = new ReportDefinition(
            "Document report",
            ["Archive number", "Title"],
            new string?[][] { ["A-001", "عقد تجريبي"] },
            "Current Explorer view");
        using var stream = new MemoryStream();

        await new ExcelExporter().ExportAsync(report, stream, CancellationToken.None);

        stream.Position = 0;
        using var document = SpreadsheetDocument.Open(stream, isEditable: false);
        Assert.Equal("Document report", document.PackageProperties.Title);
        Assert.Equal("Current Explorer view", document.PackageProperties.Description);
        Assert.Empty(new OpenXmlValidator().Validate(document));
        var text = document.WorkbookPart!.WorksheetParts.Single().Worksheet?.InnerText
            ?? throw new InvalidDataException("The exported workbook has no worksheet content.");
        Assert.Contains("Archive number", text);
        Assert.Contains("A-001", text);
        Assert.Contains("عقد تجريبي", text);
    }

    [Fact]
    public async Task PdfExporter_WritesReadablePaginatedPdfAndSplitsWideTablesIntoBands()
    {
        var columns = Enumerable.Range(1, 9).Select(index => $"Column {index}").ToArray();
        var rows = Enumerable.Range(1, 50)
            .Select(row => columns.Select((_, column) => $"R{row}C{column + 1}").ToArray())
            .ToArray();
        rows[0][0] = "وثيقة R1C1";
        var report = new ReportDefinition("Wide report", columns, rows, "Pagination test");
        using var stream = new MemoryStream();

        await new PdfExporter().ExportAsync(report, stream, CancellationToken.None);

        Assert.True(stream.Length > 1_000);
        stream.Position = 0;
        using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        Assert.True(pdf.PageCount >= 4);
        Assert.Equal("Wide report", pdf.Info.Title);

        using var readablePdf = UglyToad.PdfPig.PdfDocument.Open(stream.ToArray());
        var extractedText = string.Join('\n', readablePdf.GetPages().Select(page => page.Text));
        Assert.Contains("Wide report", extractedText);
        Assert.Contains("Column 1", extractedText);
        Assert.Contains("R1C1", extractedText);
        Assert.Contains("وثيقة", extractedText);
    }

    [Fact]
    public void ReportDefinition_RejectsRowsWithWrongCellCount()
    {
        Assert.Throws<ArgumentException>(() =>
            new ReportDefinition("Invalid", ["One", "Two"], new string?[][] { ["only one"] }));
    }

    [Fact]
    public async Task ExcelExporter_RemovesInvalidXmlCharacters()
    {
        var report = new ReportDefinition("Sanitized", ["Value"], new string?[][] { ["a\u0001b"] });
        using var stream = new MemoryStream();

        await new ExcelExporter().ExportAsync(report, stream, CancellationToken.None);

        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var worksheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml")!;
        using var reader = new StreamReader(worksheetEntry.Open());
        var xml = await reader.ReadToEndAsync();
        Assert.Equal("ab", XDocument.Parse(xml).Descendants().Single(element => element.Name.LocalName == "t" && element.Value == "ab").Value);
    }
}
