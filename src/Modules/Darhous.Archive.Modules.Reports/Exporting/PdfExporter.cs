using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace Darhous.Archive.Modules.Reports.Exporting;

public sealed class PdfExporter : IReportExporter
{
    private const string FontFamilyName = "Arial";
    private const int MaximumColumnsPerBand = 8;
    private const double Margin = 36D;
    private const double HeaderHeight = 26D;
    private const double RowHeight = 24D;

    private static readonly object FontConfigurationLock = new();
    private static bool _fontConfigurationAttempted;

    public ReportFormat Format => ReportFormat.Pdf;

    public string FileExtension => ".pdf";

    public Task ExportAsync(ReportDefinition report, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("The destination stream must be writable.", nameof(destination));
        }

        cancellationToken.ThrowIfCancellationRequested();
        ConfigureWindowsFonts();

        using var document = new PdfDocument();
        document.Info.Title = report.Title;
        document.Info.Subject = report.Subtitle ?? string.Empty;

        var titleFont = new XFont(FontFamilyName, 16D, XFontStyleEx.Bold);
        var headerFont = new XFont(FontFamilyName, 9D, XFontStyleEx.Bold);
        var bodyFont = new XFont(FontFamilyName, 9D, XFontStyleEx.Regular);

        for (var bandStart = 0; bandStart < report.Columns.Count; bandStart += MaximumColumnsPerBand)
        {
            var bandColumnCount = Math.Min(MaximumColumnsPerBand, report.Columns.Count - bandStart);
            RenderBand(document, report, bandStart, bandColumnCount, titleFont, headerFont, bodyFont, cancellationToken);
        }

        document.Save(destination, closeStream: false);
        return Task.CompletedTask;
    }

    private static void RenderBand(
        PdfDocument document,
        ReportDefinition report,
        int bandStart,
        int bandColumnCount,
        XFont titleFont,
        XFont headerFont,
        XFont bodyFont,
        CancellationToken cancellationToken)
    {
        var rowIndex = 0;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = document.AddPage();
            page.Size = PdfSharp.PageSize.A4;
            page.Orientation = PdfSharp.PageOrientation.Landscape;

            using var graphics = XGraphics.FromPdfPage(page);
            var usableWidth = page.Width.Point - (Margin * 2D);
            var columnWidth = usableWidth / bandColumnCount;
            var y = DrawPageHeading(graphics, report, bandStart, bandColumnCount, usableWidth, titleFont, bodyFont);

            DrawHeader(graphics, report, bandStart, bandColumnCount, columnWidth, y, headerFont);
            y += HeaderHeight;

            while (rowIndex < report.Rows.Count && y + RowHeight <= page.Height.Point - Margin)
            {
                DrawRow(graphics, report.Rows[rowIndex], bandStart, bandColumnCount, columnWidth, y, bodyFont);
                rowIndex++;
                y += RowHeight;
            }
        }
        while (rowIndex < report.Rows.Count);
    }

    private static double DrawPageHeading(
        XGraphics graphics,
        ReportDefinition report,
        int bandStart,
        int bandColumnCount,
        double usableWidth,
        XFont titleFont,
        XFont bodyFont)
    {
        graphics.DrawString(report.Title, titleFont, XBrushes.Black, new XRect(Margin, Margin, usableWidth, 22D), XStringFormats.TopLeft);
        var y = Margin + 28D;

        if (!string.IsNullOrWhiteSpace(report.Subtitle))
        {
            graphics.DrawString(report.Subtitle, bodyFont, XBrushes.DimGray, new XRect(Margin, y, usableWidth, 16D), XStringFormats.TopLeft);
            y += 20D;
        }

        if (report.Columns.Count > MaximumColumnsPerBand)
        {
            var bandLabel = $"Columns {bandStart + 1}-{bandStart + bandColumnCount} of {report.Columns.Count}";
            graphics.DrawString(bandLabel, bodyFont, XBrushes.DimGray, new XRect(Margin, y, usableWidth, 16D), XStringFormats.TopLeft);
            y += 20D;
        }

        return y;
    }

    private static void DrawHeader(
        XGraphics graphics,
        ReportDefinition report,
        int bandStart,
        int bandColumnCount,
        double columnWidth,
        double y,
        XFont font)
    {
        for (var index = 0; index < bandColumnCount; index++)
        {
            var rect = new XRect(Margin + (index * columnWidth), y, columnWidth, HeaderHeight);
            graphics.DrawRectangle(XPens.Gray, XBrushes.LightGray, rect);
            DrawCellText(graphics, report.Columns[bandStart + index], font, rect);
        }
    }

    private static void DrawRow(
        XGraphics graphics,
        IReadOnlyList<string?> row,
        int bandStart,
        int bandColumnCount,
        double columnWidth,
        double y,
        XFont font)
    {
        for (var index = 0; index < bandColumnCount; index++)
        {
            var rect = new XRect(Margin + (index * columnWidth), y, columnWidth, RowHeight);
            graphics.DrawRectangle(XPens.LightGray, rect);
            DrawCellText(graphics, row[bandStart + index] ?? string.Empty, font, rect);
        }
    }

    private static void DrawCellText(XGraphics graphics, string value, XFont font, XRect rect)
    {
        var singleLine = value.Replace('\r', ' ').Replace('\n', ' ');
        var maximumCharacters = Math.Max(1, (int)((rect.Width - 8D) / (font.Size * 0.55D)));
        var displayValue = singleLine.Length <= maximumCharacters
            ? singleLine
            : $"{singleLine[..Math.Max(1, maximumCharacters - 1)]}…";

        var textRect = new XRect(rect.X + 4D, rect.Y, rect.Width - 8D, rect.Height);
        graphics.DrawString(displayValue, font, XBrushes.Black, textRect, XStringFormats.CenterLeft);
    }

    private static void ConfigureWindowsFonts()
    {
        if (!OperatingSystem.IsWindows() || _fontConfigurationAttempted)
        {
            return;
        }

        lock (FontConfigurationLock)
        {
            if (_fontConfigurationAttempted)
            {
                return;
            }

            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
            _fontConfigurationAttempted = true;
        }
    }
}
