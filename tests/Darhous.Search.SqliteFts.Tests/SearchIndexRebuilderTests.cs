using Darhous.Search.SqliteFts.Contracts;

namespace Darhous.Search.SqliteFts.Tests;

public class SearchIndexRebuilderTests : SearchTestBase
{
    [Fact]
    public async Task RebuildAsync_ReindexesEveryActiveDocumentFromScratch()
    {
        var uid1 = await AddDocumentAsync("مستند أول للفهرسة");
        var uid2 = await AddDocumentAsync("مستند ثاني للفهرسة");
        await ReconciliationService.RunIncrementalSweepAsync(CancellationToken.None);

        await Rebuilder.RebuildAsync(CancellationToken.None);

        var result = await QueryService.SearchAsync(new SearchQuery("للفهرسة"), null, CancellationToken.None);
        Assert.Equal(2, result.Hits.Count);
        Assert.Contains(result.Hits, h => h.DocumentUid == uid1);
        Assert.Contains(result.Hits, h => h.DocumentUid == uid2);
    }

    [Fact]
    public async Task RebuildAsync_ExcludesTrashedDocuments()
    {
        var trashedUid = await AddDocumentAsync("مستند سيُحذف");
        await AddDocumentAsync("مستند سيبقى");
        await ReconciliationService.RunIncrementalSweepAsync(CancellationToken.None);

        await DocumentService.TrashDocumentAsync(trashedUid, null, null, CancellationToken.None);
        await Rebuilder.RebuildAsync(CancellationToken.None);

        var result = await QueryService.SearchAsync(new SearchQuery("مستند"), null, CancellationToken.None);
        Assert.Single(result.Hits);
        Assert.DoesNotContain(result.Hits, h => h.DocumentUid == trashedUid);
    }

    [Fact]
    public async Task RebuildAsync_RestoresAvailabilityAfterMarkedUnavailable()
    {
        Availability.MarkUnavailable(new InvalidOperationException("simulated corruption"));
        Assert.False(Availability.IsAvailable);

        await Rebuilder.RebuildAsync(CancellationToken.None);

        Assert.True(Availability.IsAvailable);
    }

    [Fact]
    public async Task SearchAsync_WhenUnavailable_ReturnsUnavailablePageInsteadOfThrowing()
    {
        Availability.MarkUnavailable(new InvalidOperationException("simulated corruption"));

        var result = await QueryService.SearchAsync(new SearchQuery("أي شيء"), null, CancellationToken.None);

        Assert.True(result.IsUnavailable);
        Assert.Empty(result.Hits);
    }
}
