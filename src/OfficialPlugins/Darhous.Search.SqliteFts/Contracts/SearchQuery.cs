namespace Darhous.Search.SqliteFts.Contracts;

/// <summary>DB Spec §90/§94 (Search Fields, Search Scope). <paramref name="Text"/> is raw user input — normalized and tokenized inside <c>FtsQueryService</c>, never by the caller.</summary>
public sealed record SearchQuery(
    string Text,
    Guid? FolderId = null,
    DateOnly? ArchiveDateFrom = null,
    DateOnly? ArchiveDateTo = null,
    string? FileType = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 50,
    SearchSortOrder Sort = SearchSortOrder.Relevance);
