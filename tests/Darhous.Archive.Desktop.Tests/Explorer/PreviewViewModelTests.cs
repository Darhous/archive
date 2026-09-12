using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Audit;
using Darhous.Archive.Contracts.Documents;
using Darhous.Archive.Desktop.ViewModels.Explorer.Preview;
using Darhous.Archive.Security.Sessions;

namespace Darhous.Archive.Desktop.Tests.Explorer;

public class PreviewViewModelTests
{
    private static Document MakeDocument(Guid uid, string title, Guid? versionUid = null) => new(
        uid, "ARC-2026-000001", title, null, versionUid,
        DocumentStatus.Active, DocumentSourceType.Manual, DocumentStorageMode.Managed,
        null, null, DateOnly.FromDateTime(DateTime.UtcNow), null,
        DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow, null);

    private static DocumentVersion MakeVersion(Guid uid, Guid documentUid, int versionNo, string extension, string filePath) => new(
        uid, documentUid, versionNo, $"file{extension}", $"stored{extension}", filePath, extension, null, 2048, "abc123", 3,
        null, null, DateTimeOffset.UtcNow, null, DocumentAvailabilityStatus.Available, null, null, "done", null, null, null);

    private static (PreviewViewModel ViewModel, FakeDocumentRepository Documents, FakeDocumentVersionRepository Versions, FakeAuditService Audit, FakeAuditQueryService AuditQuery) Build()
    {
        var documents = new FakeDocumentRepository();
        var versions = new FakeDocumentVersionRepository();
        var audit = new FakeAuditService();
        var auditQuery = new FakeAuditQueryService();
        var viewModel = new PreviewViewModel(documents, versions, audit, auditQuery, ArchivePrincipal.Guest);
        return (viewModel, documents, versions, audit, auditQuery);
    }

    [Fact]
    public async Task ShowDocumentAsync_NullUid_ClearsSelection()
    {
        var (vm, _, _, _, _) = Build();

        await vm.ShowDocumentAsync(null, CancellationToken.None);

        Assert.False(vm.HasSelection);
        Assert.True(vm.NoSelection);
    }

    [Fact]
    public async Task ShowDocumentAsync_KnownDocumentWithPdfVersion_PopulatesMetadataAndClassifiesPdf()
    {
        var (vm, documents, versions, _, _) = Build();
        var docUid = Guid.NewGuid();
        var versionUid = Guid.NewGuid();
        documents.AllDocuments.Add(MakeDocument(docUid, "خطاب رسمي", versionUid));
        versions.AllVersions.Add(MakeVersion(versionUid, docUid, 1, ".pdf", @"C:\archive\letter.pdf"));

        await vm.ShowDocumentAsync(docUid, CancellationToken.None);

        Assert.True(vm.HasSelection);
        Assert.Equal("خطاب رسمي", vm.Title);
        Assert.Equal(PreviewKind.Pdf, vm.Kind);
        Assert.Equal(@"C:\archive\letter.pdf", vm.FilePath);
        Assert.Equal("3", vm.PageCountDisplay);
        Assert.Equal("done", vm.ExtractionStatusDisplay);
    }

    [Fact]
    public async Task ShowDocumentAsync_OfficeExtension_ClassifiesAsOfficeFallback()
    {
        var (vm, documents, versions, _, _) = Build();
        var docUid = Guid.NewGuid();
        var versionUid = Guid.NewGuid();
        documents.AllDocuments.Add(MakeDocument(docUid, "تقرير", versionUid));
        versions.AllVersions.Add(MakeVersion(versionUid, docUid, 1, ".docx", @"C:\archive\report.docx"));

        await vm.ShowDocumentAsync(docUid, CancellationToken.None);

        Assert.Equal(PreviewKind.OfficeFallback, vm.Kind);
    }

    [Fact]
    public async Task ShowDocumentAsync_UnrecognizedExtension_ClassifiesAsUnsupported()
    {
        var (vm, documents, versions, _, _) = Build();
        var docUid = Guid.NewGuid();
        var versionUid = Guid.NewGuid();
        documents.AllDocuments.Add(MakeDocument(docUid, "ملف غريب", versionUid));
        versions.AllVersions.Add(MakeVersion(versionUid, docUid, 1, ".xyz", @"C:\archive\weird.xyz"));

        await vm.ShowDocumentAsync(docUid, CancellationToken.None);

        Assert.Equal(PreviewKind.Unsupported, vm.Kind);
    }

    [Fact]
    public async Task ShowDocumentAsync_RecordsPreviewDocumentAudit_NotOpenDocument()
    {
        var (vm, documents, _, audit, _) = Build();
        var docUid = Guid.NewGuid();
        documents.AllDocuments.Add(MakeDocument(docUid, "مستند"));

        await vm.ShowDocumentAsync(docUid, CancellationToken.None);

        var entry = Assert.Single(audit.RecordedEntries);
        Assert.Equal(AuditAction.PreviewDocument, entry.Action);
        Assert.NotEqual(AuditAction.OpenDocument, entry.Action);
    }

    [Fact]
    public async Task ShowDocumentAsync_SameDocumentTwice_DoesNotDuplicateAuditEntry()
    {
        var (vm, documents, _, audit, _) = Build();
        var docUid = Guid.NewGuid();
        documents.AllDocuments.Add(MakeDocument(docUid, "مستند"));

        await vm.ShowDocumentAsync(docUid, CancellationToken.None);
        await vm.ShowDocumentAsync(docUid, CancellationToken.None);

        Assert.Single(audit.RecordedEntries);
    }

    [Fact]
    public async Task LoadVersionsCommand_LoadsOnlyOnce_OrderedNewestFirst()
    {
        var (vm, documents, versions, _, _) = Build();
        var docUid = Guid.NewGuid();
        documents.AllDocuments.Add(MakeDocument(docUid, "مستند بعدة إصدارات"));
        versions.AllVersions.Add(MakeVersion(Guid.NewGuid(), docUid, 1, ".pdf", "v1.pdf"));
        versions.AllVersions.Add(MakeVersion(Guid.NewGuid(), docUid, 2, ".pdf", "v2.pdf"));

        await vm.ShowDocumentAsync(docUid, CancellationToken.None);
        await vm.LoadVersionsCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Versions.Count);
        Assert.Equal(2, vm.Versions[0].VersionNo);
        Assert.True(vm.VersionsLoaded);

        versions.AllVersions.Add(MakeVersion(Guid.NewGuid(), docUid, 3, ".pdf", "v3.pdf"));
        await vm.LoadVersionsCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Versions.Count); // second call is a no-op — already loaded
    }

    [Fact]
    public async Task LoadActivityCommand_FiltersByCurrentDocumentEntityUid()
    {
        var (vm, documents, _, _, auditQuery) = Build();
        var docUid = Guid.NewGuid();
        var otherDocUid = Guid.NewGuid();
        documents.AllDocuments.Add(MakeDocument(docUid, "مستند"));

        auditQuery.Events.Add(new AuditEvent(
            Guid.NewGuid(), DateTimeOffset.UtcNow, null, "admin", "Admin", AuditAction.AddDocument, AuditActionCategory.Document,
            "document", docUid.ToString(), "مستند", null, null, null, null, null, null, AuditResult.Success, null, null, null, null, "PC1"));
        auditQuery.Events.Add(new AuditEvent(
            Guid.NewGuid(), DateTimeOffset.UtcNow, null, "admin", "Admin", AuditAction.AddDocument, AuditActionCategory.Document,
            "document", otherDocUid.ToString(), "مستند آخر", null, null, null, null, null, null, AuditResult.Success, null, null, null, null, "PC1"));

        await vm.ShowDocumentAsync(docUid, CancellationToken.None);
        await vm.LoadActivityCommand.ExecuteAsync(null);

        var row = Assert.Single(vm.ActivityEntries);
        Assert.Equal(AuditAction.AddDocument, row.Action);
        Assert.True(vm.ActivityLoaded);
    }

    [Fact]
    public async Task Clear_ResetsSelectionAndLazyLoadFlags()
    {
        var (vm, documents, versions, _, _) = Build();
        var docUid = Guid.NewGuid();
        var versionUid = Guid.NewGuid();
        documents.AllDocuments.Add(MakeDocument(docUid, "مستند", versionUid));
        versions.AllVersions.Add(MakeVersion(versionUid, docUid, 1, ".pdf", "a.pdf"));
        await vm.ShowDocumentAsync(docUid, CancellationToken.None);
        await vm.LoadVersionsCommand.ExecuteAsync(null);

        vm.Clear();

        Assert.False(vm.HasSelection);
        Assert.Empty(vm.Versions);
        Assert.False(vm.VersionsLoaded);
    }
}
