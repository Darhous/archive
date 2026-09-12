namespace Darhous.Archive.Modules.Reports.SavedViews;

public sealed record SavedViewReport(
    string Name,
    string? Filter,
    IReadOnlyList<SavedViewDocument> Documents);
