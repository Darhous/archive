using Darhous.Archive.Core.Permissions;
using Darhous.Archive.Persistence.Repositories;
using Darhous.Search.SqliteFts.Contracts;

namespace Darhous.Search.SqliteFts.Tests;

public class SearchReconciliationServiceTests : SearchTestBase
{
    [Fact]
    public async Task IncrementalSweep_IndexesNewlyAddedDocument()
    {
        var uid = await AddDocumentAsync("خطاب الحماية المدنية");

        await ReconciliationService.RunIncrementalSweepAsync(CancellationToken.None);

        var result = await QueryService.SearchAsync(new SearchQuery("الحماية"), null, CancellationToken.None);

        Assert.Single(result.Hits);
        Assert.Equal(uid, result.Hits[0].DocumentUid);
    }

    [Fact]
    public async Task IncrementalSweep_IndexesExtractedBodyTextNotPresentInTitleOrFileName()
    {
        var uid = await AddDocumentAsync("محضر عام");
        await ReconciliationService.RunIncrementalSweepAsync(CancellationToken.None);
        Assert.Empty((await QueryService.SearchAsync(new SearchQuery("زرافة"), null, CancellationToken.None)).Hits);

        var document = await new DocumentRepository(ConnectionFactory).GetByUidAsync(uid, CancellationToken.None);
        Assert.NotNull(document?.CurrentVersionId);
        await UnitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.DocumentVersions.UpdateExtractionResultAsync(
                document.CurrentVersionId.Value,
                "done",
                1,
                true,
                "يتضمن هذا المستند كلمة زرافة داخل النص فقط",
                ct);
            return null;
        }, CancellationToken.None);

        await ReconciliationService.RunIncrementalSweepAsync(CancellationToken.None);
        var result = await QueryService.SearchAsync(new SearchQuery("زرافة"), null, CancellationToken.None);

        var hit = Assert.Single(result.Hits);
        Assert.Equal(uid, hit.DocumentUid);
    }

    [Fact]
    public async Task IncrementalSweep_RemovesTrashedDocumentFromActiveIndex()
    {
        var uid = await AddDocumentAsync("مذكرة داخلية");
        await ReconciliationService.RunIncrementalSweepAsync(CancellationToken.None);
        Assert.Single((await QueryService.SearchAsync(new SearchQuery("داخلية"), null, CancellationToken.None)).Hits);

        await DocumentService.TrashDocumentAsync(uid, null, null, CancellationToken.None);
        await ReconciliationService.RunIncrementalSweepAsync(CancellationToken.None);

        var result = await QueryService.SearchAsync(new SearchQuery("داخلية"), null, CancellationToken.None);
        Assert.Empty(result.Hits);
    }

    [Fact]
    public async Task IncrementalSweep_ReflectsMoveToNewFolder()
    {
        var folderResult = await FolderService.CreateFolderAsync("المرور", null, null, CancellationToken.None);
        var folderUid = folderResult.Value;

        var uid = await AddDocumentAsync("طلب رخصة قيادة");
        await ReconciliationService.RunIncrementalSweepAsync(CancellationToken.None);

        await DocumentService.MoveDocumentAsync(uid, folderUid, null, CancellationToken.None);
        await ReconciliationService.RunIncrementalSweepAsync(CancellationToken.None);

        var result = await QueryService.SearchAsync(new SearchQuery("رخصة", FolderId: folderUid), null, CancellationToken.None);
        Assert.Single(result.Hits);

        var wrongFolderResult = await QueryService.SearchAsync(new SearchQuery("رخصة", FolderId: Guid.NewGuid()), null, CancellationToken.None);
        Assert.Empty(wrongFolderResult.Hits);
    }

    [Fact]
    public async Task FullSweep_RemovesOrphanAfterPermanentDelete()
    {
        var uid = await AddDocumentAsync("مستند للحذف النهائي");
        await ReconciliationService.RunIncrementalSweepAsync(CancellationToken.None);
        Assert.Single((await QueryService.SearchAsync(new SearchQuery("النهائي"), null, CancellationToken.None)).Hits);

        // Permanent delete requires Admin role per DocumentService's own rule.
        await DocumentService.PermanentDeleteDocumentAsync(uid, UserRole.Admin, null, CancellationToken.None);

        // The incremental sweep can't see this — the row is simply gone, updated_at tells it nothing.
        await ReconciliationService.RunIncrementalSweepAsync(CancellationToken.None);
        Assert.Single((await QueryService.SearchAsync(new SearchQuery("النهائي"), null, CancellationToken.None)).Hits);

        await ReconciliationService.RunFullSweepAsync(CancellationToken.None);
        Assert.Empty((await QueryService.SearchAsync(new SearchQuery("النهائي"), null, CancellationToken.None)).Hits);
    }

    [Fact]
    public async Task FullSweep_IndexesDocumentMissedByIncrementalSweep()
    {
        // Simulates a document that exists in archive.db but was never picked up (e.g. app
        // crashed before its first sweep) — the full sweep must catch it too, not just the
        // incremental one.
        await AddDocumentAsync("مستند لم يُفهرس بعد");

        await ReconciliationService.RunFullSweepAsync(CancellationToken.None);

        var result = await QueryService.SearchAsync(new SearchQuery("يُفهرس"), null, CancellationToken.None);
        Assert.Single(result.Hits);
    }
}
