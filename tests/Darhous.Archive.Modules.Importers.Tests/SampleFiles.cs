using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Darhous.Archive.Modules.Importers.Tests;

internal static class SampleFiles
{
    /// <summary>A minimal, syntactically valid single-page PDF with a real text layer — hand-built (with a real, byte-accurate xref table; PdfPig requires one, no lenient fallback) rather than pulled from a binary fixture so the test suite has no external file dependency.</summary>
    public static string CreateSimplePdf(string directory, string text = "Hello PdfPig")
    {
        var path = Path.Combine(directory, $"{Guid.NewGuid():N}.pdf");
        var contentStream = $"BT /F1 12 Tf 10 50 Td ({text}) Tj ET";
        var objects = new[]
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/Resources<</Font<</F1 4 0 R>>>>/MediaBox[0 0 200 100]/Contents 5 0 R>>",
            "<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>",
            $"<</Length {contentStream.Length}>>\nstream\n{contentStream}\nendstream",
        };

        File.WriteAllBytes(path, BuildPdfBytes(objects));
        return path;
    }

    /// <summary>An image-only PDF (an empty content stream, no text operators) — should be detected as needing OCR.</summary>
    public static string CreateImageOnlyPdf(string directory)
    {
        var path = Path.Combine(directory, $"{Guid.NewGuid():N}.pdf");
        var objects = new[]
        {
            "<</Type/Catalog/Pages 2 0 R>>",
            "<</Type/Pages/Kids[3 0 R]/Count 1>>",
            "<</Type/Page/Parent 2 0 R/Resources<<>>/MediaBox[0 0 200 100]/Contents 4 0 R>>",
            "<</Length 0>>\nstream\n\nendstream",
        };

        File.WriteAllBytes(path, BuildPdfBytes(objects));
        return path;
    }

    /// <summary>Builds a byte-accurate PDF: header, numbered objects, a correct xref offset table, and a trailer pointing at the catalog (object 1).</summary>
    private static byte[] BuildPdfBytes(IReadOnlyList<string> objectBodies)
    {
        var buffer = new List<byte>();
        var offsets = new List<int>();

        void AppendAscii(string text) => buffer.AddRange(System.Text.Encoding.ASCII.GetBytes(text));

        AppendAscii("%PDF-1.4\n");

        for (var i = 0; i < objectBodies.Count; i++)
        {
            offsets.Add(buffer.Count);
            AppendAscii($"{i + 1} 0 obj\n{objectBodies[i]}\nendobj\n");
        }

        var xrefOffset = buffer.Count;
        var xref = new System.Text.StringBuilder();
        xref.Append($"xref\n0 {objectBodies.Count + 1}\n");
        xref.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            xref.Append($"{offset:D10} 00000 n \n");
        }

        AppendAscii(xref.ToString());
        AppendAscii($"trailer\n<</Size {objectBodies.Count + 1}/Root 1 0 R>>\nstartxref\n{xrefOffset}\n%%EOF");

        return buffer.ToArray();
    }

    public static string CreateDocx(string directory, string text)
    {
        var path = Path.Combine(directory, $"{Guid.NewGuid():N}.docx");
        using var document = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new W.Document(new W.Body(new W.Paragraph(new W.Run(new W.Text(text)))));
        mainPart.Document.Save();
        return path;
    }

    public static string CreatePptx(string directory, string slideText)
    {
        var path = Path.Combine(directory, $"{Guid.NewGuid():N}.pptx");
        using var document = PresentationDocument.Create(path, DocumentFormat.OpenXml.PresentationDocumentType.Presentation);
        var presentationPart = document.AddPresentationPart();
        presentationPart.Presentation = new P.Presentation();

        var slidePart = presentationPart.AddNewPart<SlidePart>();
        slidePart.Slide = new P.Slide(
            new P.CommonSlideData(
                new P.ShapeTree(
                    new P.Shape(
                        new P.NonVisualShapeProperties(
                            new P.NonVisualDrawingProperties { Id = 2, Name = "TextBox" },
                            new P.NonVisualShapeDrawingProperties(),
                            new P.ApplicationNonVisualDrawingProperties()),
                        new P.ShapeProperties(),
                        new P.TextBody(
                            new A.BodyProperties(),
                            new A.Paragraph(new A.Run(new A.Text(slideText))))))));

        var slideIdList = new P.SlideIdList(new P.SlideId { Id = 256, RelationshipId = presentationPart.GetIdOfPart(slidePart) });
        presentationPart.Presentation.Append(slideIdList);
        presentationPart.Presentation.Save();
        return path;
    }

    public static string CreateXlsx(string directory, string cellText)
    {
        var path = Path.Combine(directory, $"{Guid.NewGuid():N}.xlsx");
        using var document = SpreadsheetDocument.Create(path, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();

        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var row = new Row { RowIndex = 1 };
        row.Append(new Cell { CellReference = "A1", DataType = CellValues.InlineString, InlineString = new InlineString(new Text(cellText)) });
        worksheetPart.Worksheet = new Worksheet(new SheetData(row));

        var sheets = workbookPart.Workbook.AppendChild(new Sheets());
        sheets.AppendChild(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = 1, Name = "Sheet1" });
        workbookPart.Workbook.Save();
        return path;
    }

    public static string CreateEml(string directory, string subject, string from, string to, string body)
    {
        var path = Path.Combine(directory, $"{Guid.NewGuid():N}.eml");
        var eml =
            $"""
            From: {from}
            To: {to}
            Subject: {subject}
            Date: Mon, 1 Sep 2026 10:00:00 +0000
            Content-Type: text/plain; charset="utf-8"

            {body}
            """;

        File.WriteAllText(path, eml);
        return path;
    }
}
