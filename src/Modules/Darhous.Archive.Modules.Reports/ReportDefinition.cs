namespace Darhous.Archive.Modules.Reports;

public sealed class ReportDefinition
{
    public ReportDefinition(
        string title,
        IEnumerable<string> columns,
        IEnumerable<IEnumerable<string?>> rows,
        string? subtitle = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);

        var columnSnapshot = columns.ToArray();
        if (columnSnapshot.Length == 0)
        {
            throw new ArgumentException("A report must have at least one column.", nameof(columns));
        }

        if (columnSnapshot.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Report column headers cannot be empty.", nameof(columns));
        }

        var rowSnapshot = rows.Select(row =>
        {
            ArgumentNullException.ThrowIfNull(row);
            return (IReadOnlyList<string?>)row.ToArray();
        }).ToArray();

        if (rowSnapshot.Any(row => row.Count != columnSnapshot.Length))
        {
            throw new ArgumentException("Every report row must have the same number of cells as the column list.", nameof(rows));
        }

        Title = title;
        Subtitle = subtitle;
        Columns = columnSnapshot;
        Rows = rowSnapshot;
    }

    public string Title { get; }

    public string? Subtitle { get; }

    public IReadOnlyList<string> Columns { get; }

    public IReadOnlyList<IReadOnlyList<string?>> Rows { get; }
}
