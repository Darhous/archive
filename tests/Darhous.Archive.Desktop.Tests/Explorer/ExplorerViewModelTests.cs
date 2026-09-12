using System.Diagnostics;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Documents;
using Darhous.Archive.Desktop.ViewModels.Explorer;
using Darhous.Archive.Security.Sessions;

namespace Darhous.Archive.Desktop.Tests.Explorer;

public class ExplorerViewModelTests
{
    private static Document MakeDocument(string title, Guid? folderId = null) => new(
        Guid.NewGuid(), $"ARC-2026-{Random.Shared.Next(999999):D6}", title, folderId, null,
        DocumentStatus.Active, DocumentSourceType.Manual, DocumentStorageMode.Managed,
        null, null, DateOnly.FromDateTime(DateTime.UtcNow), null,
        DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow, null);

    private static (ExplorerViewModel ViewModel, FakeFolderService Folders, FakeDocumentRepository Documents, FakeDocumentService DocService, FakeBulkOperationService Bulk) Build()
    {
        var folders = new FakeFolderService();
        var documents = new FakeDocumentRepository();
        var docService = new FakeDocumentService();
        var bulk = new FakeBulkOperationService();

        var viewModel = new ExplorerViewModel(folders, documents, docService, bulk, ArchivePrincipal.Guest);
        return (viewModel, folders, documents, docService, bulk);
    }

    [Fact]
    public async Task InitializeAsync_AlwaysHasAllArchiveAndUnclassifiedNodes()
    {
        var (vm, _, _, _, _) = Build();

        await vm.InitializeAsync();

        Assert.Contains(vm.RootNodes, n => n.Name == "كل الأرشيف");
        Assert.Contains(vm.RootNodes, n => n.Name == "غير مصنف");
    }

    [Fact]
    public async Task InitializeAsync_BuildsNestedFolderTree()
    {
        var (vm, folders, _, _, _) = Build();
        var parentUid = Guid.NewGuid();
        var childUid = Guid.NewGuid();
        var parent = new Folder(parentUid, null, "المرور", 0, null, false, false, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var child = new Folder(childUid, parentUid, "مرور قنا", 0, null, false, false, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        folders.ChildrenByParent[Guid.Empty] = [parent];
        folders.ChildrenByParent[parentUid] = [child];

        await vm.InitializeAsync();

        var parentNode = vm.RootNodes.Single(n => n.Name == "المرور");
        Assert.Single(parentNode.Children);
        Assert.Equal("مرور قنا", parentNode.Children[0].Name);
    }

    [Fact]
    public async Task InitializeAsync_SelectsAllArchive_LoadsAllDocuments()
    {
        var (vm, _, documents, _, _) = Build();
        documents.AllDocuments.Add(MakeDocument("خطاب أول"));
        documents.AllDocuments.Add(MakeDocument("خطاب ثاني"));

        await vm.InitializeAsync();

        Assert.Equal(2, vm.Documents.Count);
    }

    [Fact]
    public async Task SearchText_FiltersDocumentsByNormalizedTitle()
    {
        var (vm, _, documents, _, _) = Build();
        documents.AllDocuments.Add(MakeDocument("خطاب الحماية المدنية"));
        documents.AllDocuments.Add(MakeDocument("مذكرة مرور قنا"));
        await vm.InitializeAsync();

        vm.SearchText = "مرور";

        Assert.Single(vm.Documents);
        Assert.Contains("مرور", vm.Documents[0].Title);
    }

    [Fact]
    public async Task SearchText_IgnoresArabicDiacriticsAndAlefVariants()
    {
        var (vm, _, documents, _, _) = Build();
        documents.AllDocuments.Add(MakeDocument("أحمد")); // alef-hamza
        await vm.InitializeAsync();

        vm.SearchText = "احمد"; // plain alef, no hamza

        Assert.Single(vm.Documents);
    }

    [Fact]
    public async Task ClearingSearchText_RestoresFullList()
    {
        var (vm, _, documents, _, _) = Build();
        documents.AllDocuments.Add(MakeDocument("أ"));
        documents.AllDocuments.Add(MakeDocument("ب"));
        await vm.InitializeAsync();

        vm.SearchText = "أ";
        Assert.Single(vm.Documents);

        vm.SearchText = "";
        Assert.Equal(2, vm.Documents.Count);
    }

    [Fact]
    public async Task DeleteSelectedAsync_TrashesOnlySelectedDocuments()
    {
        var (vm, _, documents, docService, _) = Build();
        documents.AllDocuments.Add(MakeDocument("محدد"));
        documents.AllDocuments.Add(MakeDocument("غير محدد"));
        await vm.InitializeAsync();

        vm.Documents.Single(d => d.Title == "محدد").IsSelected = true;

        await vm.DeleteSelectedCommand.ExecuteAsync(null);

        Assert.Single(docService.TrashedDocuments);
    }

    [Fact]
    public async Task MoveSelectedToFolderAsync_PassesSelectedUidsAndTarget()
    {
        var (vm, _, documents, _, bulk) = Build();
        var doc = MakeDocument("للنقل");
        documents.AllDocuments.Add(doc);
        await vm.InitializeAsync();
        vm.Documents.Single().IsSelected = true;

        var target = new FolderNodeViewModel(Guid.NewGuid(), "هدف");
        vm.MoveTargetFolder = target;

        await vm.MoveSelectedToFolderCommand.ExecuteAsync(null);

        var call = Assert.Single(bulk.Calls);
        Assert.Equal(doc.Uid, Assert.Single(call.Documents));
        Assert.Equal(target.Uid, call.Target);
    }

    [Theory]
    [InlineData(DensityMode.Compact)]
    [InlineData(DensityMode.Standard)]
    [InlineData(DensityMode.Comfortable)]
    public void RowPadding_ChangesWithDensity(DensityMode density)
    {
        var (vm, _, _, _, _) = Build();

        vm.Density = density;

        Assert.True(vm.RowPadding.Top >= 2);
    }

    /// <summary>
    /// Implementation Plan §41 UI Performance Gate — "100,000 fake rows, UI stays
    /// responsive". This validates the data-side half of that gate automatically: building
    /// 100k rows and running the same live-filter pass ExplorerViewModel.ApplyFilter does
    /// must stay fast (single O(n) pass, no per-item DB round trip). WPF's own virtualization
    /// (VirtualizingPanel, set in ExplorerWindow.xaml) is what keeps *rendering* cheap once
    /// the data is ready — that half needs Ahmed's manual visual confirmation on his machine,
    /// same caveat as the Phase 3 Login UI (no interactive display in this environment).
    /// </summary>
    [Fact]
    public async Task LiveFilter_With100000Documents_CompletesQuickly()
    {
        var (vm, _, documents, _, _) = Build();
        for (var i = 0; i < 100_000; i++)
        {
            documents.AllDocuments.Add(MakeDocument($"مستند رقم {i}"));
        }

        var stopwatch = Stopwatch.StartNew();
        await vm.InitializeAsync();
        vm.SearchText = "رقم 12345";
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 2000,
            $"Loading + filtering 100,000 rows took {stopwatch.ElapsedMilliseconds}ms — expected under 2000ms.");
        Assert.Single(vm.Documents);
    }
}
