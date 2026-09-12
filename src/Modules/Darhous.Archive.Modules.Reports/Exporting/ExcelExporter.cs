using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Xml;

namespace Darhous.Archive.Modules.Reports.Exporting;

public sealed class ExcelExporter : IReportExporter
{
    private const int MaximumColumns = 16_384;
    private const int MaximumDataRows = 1_048_575;

    public ReportFormat Format => ReportFormat.Excel;

    public string FileExtension => ".xlsx";

    public Task ExportAsync(ReportDefinition report, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite || !destination.CanSeek)
        {
            throw new ArgumentException("The Excel destination stream must be writable and seekable.", nameof(destination));
        }

        if (report.Columns.Count > MaximumColumns)
        {
            throw new ArgumentOutOfRangeException(nameof(report), $"Excel supports at most {MaximumColumns:N0} columns.");
        }

        if (report.Rows.Count > MaximumDataRows)
        {
            throw new ArgumentOutOfRangeException(nameof(report), $"Excel supports at most {MaximumDataRows:N0} data rows when a header row is present.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var document = SpreadsheetDocument.Create(destination, SpreadsheetDocumentType.Workbook, autoSave: true);
        document.PackageProperties.Title = report.Title;
        document.PackageProperties.Description = report.Subtitle;

        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        AddStyles(workbookPart);

        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        var sheetViews = new SheetViews(
            new SheetView(
                new Pane
                {
                    VerticalSplit = 1D,
                    TopLeftCell = "A2",
                    ActivePane = PaneValues.BottomLeft,
                    State = PaneStateValues.Frozen,
                })
            { WorkbookViewId = 0U });
        var worksheet = new Worksheet(sheetViews, CreateColumns(report), sheetData);
        worksheetPart.Worksheet = worksheet;

        sheetData.Append(CreateRow(report.Columns, styleIndex: 1));
        foreach (var row in report.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sheetData.Append(CreateRow(row, styleIndex: 0));
        }

        var lastColumn = GetColumnName(report.Columns.Count);
        worksheet.Append(new AutoFilter { Reference = $"A1:{lastColumn}{report.Rows.Count + 1}" });

        var sheets = workbookPart.Workbook.AppendChild(new Sheets());
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1U,
            Name = "Report",
        });

        workbookPart.Workbook.Save();
        return Task.CompletedTask;
    }

    private static Columns CreateColumns(ReportDefinition report)
    {
        var columns = new Columns();
        for (var index = 0; index < report.Columns.Count; index++)
        {
            var longestCell = report.Rows
                .Select(row => row[index]?.Length ?? 0)
                .Prepend(report.Columns[index].Length)
                .Max();

            columns.Append(new Column
            {
                Min = (uint)(index + 1),
                Max = (uint)(index + 1),
                Width = Math.Clamp(longestCell + 2D, 10D, 60D),
                CustomWidth = true,
            });
        }

        return columns;
    }

    private static Row CreateRow(IEnumerable<string?> values, uint styleIndex)
    {
        var row = new Row();
        foreach (var value in values)
        {
            var text = new Text(RemoveInvalidXmlCharacters(value ?? string.Empty))
            {
                Space = SpaceProcessingModeValues.Preserve,
            };
            row.Append(new Cell(new InlineString(text))
            {
                DataType = CellValues.InlineString,
                StyleIndex = styleIndex,
            });
        }

        return row;
    }

    private static string RemoveInvalidXmlCharacters(string value) =>
        string.Concat(value.Where(XmlConvert.IsXmlChar));

    private static string GetColumnName(int oneBasedColumnNumber)
    {
        var name = string.Empty;
        var value = oneBasedColumnNumber;
        while (value > 0)
        {
            value--;
            name = (char)('A' + (value % 26)) + name;
            value /= 26;
        }

        return name;
    }

    private static void AddStyles(WorkbookPart workbookPart)
    {
        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = new Stylesheet(
            new Fonts(
                new Font(),
                new Font(new Bold()))
            { Count = 2U },
            new Fills(
                new Fill(new PatternFill { PatternType = PatternValues.None }),
                new Fill(new PatternFill { PatternType = PatternValues.Gray125 }))
            { Count = 2U },
            new Borders(new Border()) { Count = 1U },
            new CellStyleFormats(new CellFormat()) { Count = 1U },
            new CellFormats(
                new CellFormat(),
                new CellFormat { FontId = 1U, ApplyFont = true })
            { Count = 2U });
        stylesPart.Stylesheet.Save();
    }
}
