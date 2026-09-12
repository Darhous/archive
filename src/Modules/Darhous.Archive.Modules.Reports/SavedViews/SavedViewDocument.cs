namespace Darhous.Archive.Modules.Reports.SavedViews;

public sealed record SavedViewDocument(
    string ArchiveNumber,
    string Title,
    string Status,
    string ArchiveDate);
