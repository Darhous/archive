using Darhous.Archive.Contracts.Documents;

namespace Darhous.Archive.Modules.Discovery.Tests;

/// <summary>End-to-end: Onboarding-style watch folder → DiscoveryScanJob → FileIndexJob → a real document, exercising the whole job pipeline together rather than each piece in isolation.</summary>
public class DiscoveryPipelineTests : DiscoveryTestBase
{
    [Fact]
    public async Task StartInitialDiscoveryAsync_NoWatchFolders_Fails()
    {
        var result = await Orchestrator.StartInitialDiscoveryAsync(null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("DISCOVERY_NO_WATCH_FOLDERS", result.Error!.Code);
    }

    [Fact]
    public async Task InitialDiscovery_IndexInPlaceWatchFolder_EndsWithIndexedInPlaceDocument()
    {
        CreateFile("report.pdf");
        await WatchFolderService.AddAsync(ScanRoot, true, "index_in_place", null, null, CancellationToken.None);

        var startResult = await Orchestrator.StartInitialDiscoveryAsync(null, CancellationToken.None);
        Assert.True(startResult.IsSuccess);

        await DrainJobsAsync();

        var documents = await DocumentRepository.ListAsync(CancellationToken.None);
        var document = Assert.Single(documents);
        Assert.Equal("report", document.Title);
        Assert.Equal(DocumentStorageMode.IndexedInPlace, document.StorageMode);
        Assert.Equal(DocumentSourceType.WatchFolder, document.SourceType);
        Assert.Equal(DocumentStatus.Active, document.Status);
    }

    [Fact]
    public async Task InitialDiscovery_RespectsTechnicalAndUserExclusions()
    {
        await ExclusionService.SeedTechnicalExclusionsAsync(CancellationToken.None);
        var excludedFolder = Path.Combine(ScanRoot, "excluded");
        Directory.CreateDirectory(excludedFolder);
        CreateFile(Path.Combine("excluded", "hidden.pdf"));
        CreateFile("visible.pdf");
        await ExclusionService.AddUserExclusionAsync("folder", excludedFolder, null, null, CancellationToken.None);

        await WatchFolderService.AddAsync(ScanRoot, true, "index_in_place", null, null, CancellationToken.None);
        await Orchestrator.StartInitialDiscoveryAsync(null, CancellationToken.None);
        await DrainJobsAsync();

        var documents = await DocumentRepository.ListAsync(CancellationToken.None);
        var document = Assert.Single(documents);
        Assert.Equal("visible", document.Title);
    }

    [Fact]
    public async Task InitialDiscovery_DoesNotReindexAlreadyDiscoveredFile_OnASecondRun()
    {
        CreateFile("report.pdf");
        await WatchFolderService.AddAsync(ScanRoot, true, "index_in_place", null, null, CancellationToken.None);

        await Orchestrator.StartInitialDiscoveryAsync(null, CancellationToken.None);
        await DrainJobsAsync();

        await Orchestrator.StartInitialDiscoveryAsync(null, CancellationToken.None);
        await DrainJobsAsync();

        var documents = await DocumentRepository.ListAsync(CancellationToken.None);
        Assert.Single(documents);
    }

    [Fact]
    public async Task InitialDiscovery_ManagedCopyWatchFolder_CopiesIntoArchiveStorage()
    {
        var sourcePath = CreateFile("contract.pdf");
        await WatchFolderService.AddAsync(ScanRoot, true, "managed_copy", null, null, CancellationToken.None);

        await Orchestrator.StartInitialDiscoveryAsync(null, CancellationToken.None);
        await DrainJobsAsync();

        var documents = await DocumentRepository.ListAsync(CancellationToken.None);
        var document = Assert.Single(documents);
        Assert.Equal(DocumentStorageMode.Managed, document.StorageMode);
        Assert.True(File.Exists(sourcePath), "managed_copy must leave the original file in place.");
    }
}
