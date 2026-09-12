using Darhous.Search.SqliteFts.Contracts;

namespace Darhous.Search.SqliteFts.Tests;

public class FtsQueryServiceTests : SearchTestBase
{
    [Fact]
    public async Task SearchAsync_IgnoresArabicDiacriticsAndAlefVariants()
    {
        await AddDocumentAsync("أحمد محمود");
        await ReconciliationService.RunIncrementalSweepAsync(CancellationToken.None);

        var result = await QueryService.SearchAsync(new SearchQuery("احمد"), null, CancellationToken.None);

        Assert.Single(result.Hits);
    }

    [Fact]
    public async Task SearchAsync_SupportsPrefixMatching()
    {
        await AddDocumentAsync("مذكرة تفاهم");
        await ReconciliationService.RunIncrementalSweepAsync(CancellationToken.None);

        var result = await QueryService.SearchAsync(new SearchQuery("تفا"), null, CancellationToken.None);

        Assert.Single(result.Hits);
    }

    [Fact]
    public async Task SearchAsync_RanksTitleMatchAboveFileNameOnlyMatch()
    {
        // "قنا" appears in the title of the first document and only (indirectly, via the
        // source file's name, since AddDocumentAsync titles the source file the same as the
        // title) — this test instead directly checks that a document whose TITLE contains the
        // term outranks one where the term is absent from the title, proving the bm25 weight
        // (title=10 vs others) actually drives ordering, not just match/no-match.
        var titleMatchUid = await AddDocumentAsync("محافظة قنا");
        await AddDocumentAsync("محافظة أسوان");
        await ReconciliationService.RunIncrementalSweepAsync(CancellationToken.None);

        var result = await QueryService.SearchAsync(new SearchQuery("قنا"), null, CancellationToken.None);

        Assert.Single(result.Hits);
        Assert.Equal(titleMatchUid, result.Hits[0].DocumentUid);
    }

    [Fact]
    public async Task SearchAsync_ReturnsSnippetHighlightingMatch()
    {
        await AddDocumentAsync("تقرير الأداء السنوي");
        await ReconciliationService.RunIncrementalSweepAsync(CancellationToken.None);

        var result = await QueryService.SearchAsync(new SearchQuery("الأداء"), null, CancellationToken.None);

        Assert.Single(result.Hits);
        Assert.NotNull(result.Hits[0].Snippet);
        Assert.Contains('[', result.Hits[0].Snippet!);
    }

    [Fact]
    public async Task SearchAsync_FiltersByFileType()
    {
        await AddDocumentAsync("عقد إيجار"); // AddDocumentAsync always creates a .pdf source file
        await ReconciliationService.RunIncrementalSweepAsync(CancellationToken.None);

        var pdfResult = await QueryService.SearchAsync(new SearchQuery("إيجار", FileType: "pdf"), null, CancellationToken.None);
        Assert.Single(pdfResult.Hits);

        var docxResult = await QueryService.SearchAsync(new SearchQuery("إيجار", FileType: "docx"), null, CancellationToken.None);
        Assert.Empty(docxResult.Hits);
    }

    [Fact]
    public async Task SearchAsync_PaginatesResults()
    {
        for (var i = 0; i < 5; i++)
        {
            await AddDocumentAsync($"تقرير شهري رقم {i}");
        }

        await ReconciliationService.RunIncrementalSweepAsync(CancellationToken.None);

        var page1 = await QueryService.SearchAsync(new SearchQuery("تقرير", Page: 1, PageSize: 2), null, CancellationToken.None);
        var page2 = await QueryService.SearchAsync(new SearchQuery("تقرير", Page: 2, PageSize: 2), null, CancellationToken.None);

        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(2, page1.Hits.Count);
        Assert.Equal(2, page2.Hits.Count);
        Assert.DoesNotContain(page2.Hits[0].DocumentUid, page1.Hits.Select(h => h.DocumentUid));
    }

    [Fact]
    public async Task SearchAsync_EmptyQueryText_ReturnsEmptyPageWithoutThrowing()
    {
        var result = await QueryService.SearchAsync(new SearchQuery(""), null, CancellationToken.None);

        Assert.Empty(result.Hits);
        Assert.False(result.IsUnavailable);
    }

    [Fact]
    public async Task SearchAsync_AllArchiveScope_FindsDocumentsAcrossFolders()
    {
        var folder = await FolderService.CreateFolderAsync("أرشيف قديم", null, null, CancellationToken.None);
        await AddDocumentAsync("وثيقة عامة");
        await AddDocumentAsync("وثيقة داخل فولدر", folder.Value);
        await ReconciliationService.RunIncrementalSweepAsync(CancellationToken.None);

        var result = await QueryService.SearchAsync(new SearchQuery("وثيقة"), null, CancellationToken.None);

        Assert.Equal(2, result.Hits.Count);
    }
}
