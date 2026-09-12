using System.Text;

namespace Darhous.Archive.Modules.Reports.Exporting;

public sealed class CsvExporter : IReportExporter
{
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    public ReportFormat Format => ReportFormat.Csv;

    public string FileExtension => ".csv";

    public async Task ExportAsync(ReportDefinition report, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
        {
            throw new ArgumentException("The destination stream must be writable.", nameof(destination));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var writer = new StreamWriter(destination, Utf8WithBom, bufferSize: 1024, leaveOpen: true);
        await writer.WriteLineAsync(string.Join(',', report.Columns.Select(Escape)).AsMemory(), cancellationToken);

        foreach (var row in report.Rows)
        {
            await writer.WriteLineAsync(string.Join(',', row.Select(Escape)).AsMemory(), cancellationToken);
        }

        await writer.FlushAsync(cancellationToken);
    }

    private static string Escape(string? value)
    {
        var safeValue = PreventFormulaExecution(value ?? string.Empty);
        if (safeValue.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return safeValue;
        }

        return $"\"{safeValue.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string PreventFormulaExecution(string value) =>
        value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r'
            ? $"'{value}"
            : value;
}
