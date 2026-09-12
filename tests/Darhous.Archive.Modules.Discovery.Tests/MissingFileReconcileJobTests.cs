using Darhous.Archive.Contracts.Documents;

namespace Darhous.Archive.Modules.Discovery.Tests;

public class MissingFileReconcileJobTests : DiscoveryTestBase
{
    [Fact]
    public async Task Reconcile_FileDeletedAfterIndexing_MarksDocumentMissing()
    {
        var path = CreateFile("gone-soon.pdf");
        await WatchFolderService.AddAsync(ScanRoot, true, "index_in_place", null, null, CancellationToken.None);
        await Orchestrator.StartInitialDiscoveryAsync(null, CancellationToken.None);
        await DrainJobsAsync();

        File.Delete(path);

        await UnitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.Jobs.CreateAsync(new Darhous.Archive.Application.Persistence.NewJob(
                nameof(Darhous.Archive.Modules.Discovery.Jobs.MissingFileReconcileJob), "Tests", null, null, null, 0, 0, null), ct);
            return null;
        }, CancellationToken.None);
        await DrainJobsAsync(maxPasses: 1);

        var documents = await DocumentRepository.ListAsync(CancellationToken.None);
        var document = Assert.Single(documents);
        Assert.Equal(DocumentStatus.Missing, document.Status);
    }

    [Fact]
    public async Task Reconcile_FileStillPresent_LeavesDocumentActive()
    {
        CreateFile("still-here.pdf");
        await WatchFolderService.AddAsync(ScanRoot, true, "index_in_place", null, null, CancellationToken.None);
        await Orchestrator.StartInitialDiscoveryAsync(null, CancellationToken.None);
        await DrainJobsAsync();

        await UnitOfWork.ExecuteAsync<object?>(async (context, ct) =>
        {
            await context.Jobs.CreateAsync(new Darhous.Archive.Application.Persistence.NewJob(
                nameof(Darhous.Archive.Modules.Discovery.Jobs.MissingFileReconcileJob), "Tests", null, null, null, 0, 0, null), ct);
            return null;
        }, CancellationToken.None);
        await DrainJobsAsync(maxPasses: 1);

        var documents = await DocumentRepository.ListAsync(CancellationToken.None);
        var document = Assert.Single(documents);
        Assert.Equal(DocumentStatus.Active, document.Status);
    }
}
