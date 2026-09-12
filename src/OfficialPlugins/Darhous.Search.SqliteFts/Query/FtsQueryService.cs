using Dapper;
using Darhous.Archive.Contracts.Audit;
using Darhous.Archive.Core.Audit;
using Darhous.Archive.Core.Text;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Search.SqliteFts.Availability;
using Darhous.Search.SqliteFts.Contracts;
using Microsoft.Extensions.Logging;

namespace Darhous.Search.SqliteFts.Query;

/// <summary>
/// DB Spec §90-94. Every term is wrapped as a quoted FTS5 prefix token (<c>"term"*</c>) —
/// quoting neutralizes any FTS5 syntax characters the user types (SAD §90 lists no query
/// syntax as a supported feature, so nothing is lost), and the prefix wildcard is what makes
/// the <c>prefix = '2 3 4'</c> index setting (Migration doc comment) actually useful.
/// </summary>
public sealed class FtsQueryService(
    ISqliteConnectionFactory connectionFactory, ISearchAvailability availability,
    IAuditService auditService, ILogger<FtsQueryService> logger)
    : IFtsQueryService
{
    private const string BmWeights = "10.0, 8.0, 5.0, 1.0"; // title, file_name, metadata, body — DB Spec §92; order must match documents_fts' indexed column order exactly.

    public async Task<SearchResultPage> SearchAsync(SearchQuery query, Guid? userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Text))
        {
            return new SearchResultPage([], 0, query.Page, query.PageSize);
        }

        if (!availability.IsAvailable)
        {
            return new SearchResultPage([], 0, query.Page, query.PageSize) { IsUnavailable = true };
        }

        var matchExpression = BuildMatchExpression(query.Text);
        var (whereSql, parameters) = BuildFilters(query, matchExpression);
        var orderBySql = BuildOrderBy(query.Sort);
        var offset = Math.Max(0, (query.Page - 1) * query.PageSize);

        parameters.Add("Limit", query.PageSize);
        parameters.Add("Offset", offset);

        try
        {
            await using var connection = await connectionFactory.OpenAsync(DatabaseKind.Search, cancellationToken);

            var totalCount = await connection.QuerySingleAsync<int>(new CommandDefinition(
                $"""
                SELECT COUNT(*)
                FROM documents_fts
                JOIN search_documents sd ON sd.document_id = documents_fts.rowid
                WHERE {whereSql};
                """,
                parameters, cancellationToken: cancellationToken));

            var rows = await connection.QueryAsync<SearchHitRow>(new CommandDefinition(
                $"""
                SELECT sd.document_uid AS DocumentUid, sd.archive_number AS ArchiveNumber, sd.title_display AS Title,
                       sd.folder_id AS FolderId, sd.archive_date AS ArchiveDate, sd.status AS Status,
                       bm25(documents_fts, {BmWeights}) AS Rank,
                       snippet(documents_fts, -1, '[', ']', '…', 10) AS Snippet
                FROM documents_fts
                JOIN search_documents sd ON sd.document_id = documents_fts.rowid
                WHERE {whereSql}
                ORDER BY {orderBySql}
                LIMIT @Limit OFFSET @Offset;
                """,
                parameters, cancellationToken: cancellationToken));

            var hits = rows.Select(r => new SearchHit(
                Guid.Parse(r.DocumentUid), r.ArchiveNumber, r.Title, r.Snippet,
                r.FolderId is null ? null : Guid.Parse(r.FolderId), DateOnly.Parse(r.ArchiveDate), r.Status, r.Rank)).ToList();

            availability.MarkAvailable();

            await auditService.RecordAsync(
                new AuditEntry(
                    AuditAction.Search, AuditActionCategory.Search, AuditResult.Success,
                    UserId: userId, SearchQuery: query.Text, FilterExpression: BuildFilterSummary(query)),
                cancellationToken);

            return new SearchResultPage(hits, totalCount, query.Page, query.PageSize);
        }
        catch (Exception ex) when (SearchAvailability.IsCorruption(ex))
        {
            availability.MarkUnavailable(ex);
            return new SearchResultPage([], 0, query.Page, query.PageSize) { IsUnavailable = true };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search query failed for {Query}", query);
            return new SearchResultPage([], 0, query.Page, query.PageSize) { IsUnavailable = true };
        }
    }

    private static string? BuildFilterSummary(SearchQuery query)
    {
        var parts = new List<string>();
        if (query.FolderId is { } folderId) parts.Add($"folder={folderId}");
        if (query.ArchiveDateFrom is { } from) parts.Add($"from={from:yyyy-MM-dd}");
        if (query.ArchiveDateTo is { } to) parts.Add($"to={to:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(query.FileType)) parts.Add($"fileType={query.FileType}");
        if (!string.IsNullOrWhiteSpace(query.Status)) parts.Add($"status={query.Status}");
        return parts.Count == 0 ? null : string.Join(';', parts);
    }

    private static string BuildMatchExpression(string text)
    {
        var normalized = ArabicNormalization.Normalize(text);
        var terms = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", terms.Select(t => $"\"{t.Replace("\"", "\"\"")}\"*"));
    }

    private static (string WhereSql, DynamicParameters Parameters) BuildFilters(SearchQuery query, string matchExpression)
    {
        var conditions = new List<string> { "documents_fts MATCH @Match", "sd.is_searchable = 1" };
        var parameters = new DynamicParameters();
        parameters.Add("Match", matchExpression);

        if (query.FolderId is { } folderId)
        {
            conditions.Add("sd.folder_id = @FolderId");
            parameters.Add("FolderId", folderId.ToString());
        }

        if (query.ArchiveDateFrom is { } from)
        {
            conditions.Add("sd.archive_date >= @DateFrom");
            parameters.Add("DateFrom", from.ToString("yyyy-MM-dd"));
        }

        if (query.ArchiveDateTo is { } to)
        {
            conditions.Add("sd.archive_date <= @DateTo");
            parameters.Add("DateTo", to.ToString("yyyy-MM-dd"));
        }

        if (!string.IsNullOrWhiteSpace(query.FileType))
        {
            conditions.Add("sd.file_type = @FileType");
            parameters.Add("FileType", query.FileType.ToLowerInvariant());
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            conditions.Add("sd.status = @Status");
            parameters.Add("Status", query.Status);
        }

        return (string.Join(" AND ", conditions), parameters);
    }

    private static string BuildOrderBy(SearchSortOrder sort) => sort switch
    {
        SearchSortOrder.DateDescending => "sd.archive_date DESC",
        SearchSortOrder.DateAscending => "sd.archive_date ASC",
        SearchSortOrder.TitleAscending => "sd.title_display ASC",
        _ => $"bm25(documents_fts, {BmWeights}) ASC", // lower bm25 = more relevant
    };

    private sealed class SearchHitRow
    {
        public string DocumentUid { get; set; } = "";
        public string ArchiveNumber { get; set; } = "";
        public string Title { get; set; } = "";
        public string? FolderId { get; set; }
        public string ArchiveDate { get; set; } = "";
        public string Status { get; set; } = "";
        public double Rank { get; set; }
        public string? Snippet { get; set; }
    }
}
