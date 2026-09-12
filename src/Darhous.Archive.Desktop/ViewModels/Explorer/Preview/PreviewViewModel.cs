using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Audit;
using Darhous.Archive.Core.Audit;
using Darhous.Archive.Security.Sessions;

namespace Darhous.Archive.Desktop.ViewModels.Explorer.Preview;

/// <summary>
/// SAD §21 (Preview Pane). Implementation Plan §60 — showing a document here logs
/// <c>preview_document</c>, never <c>open_document</c> (that's reserved for actually launching
/// the file in its own app, e.g. via <see cref="OpenExternallyCommand"/>). Versions/Activity
/// are lazy-loaded (§59) — only fetched the first time their tab is actually selected.
/// </summary>
public sealed partial class PreviewViewModel(
    IDocumentRepository documentRepository, IDocumentVersionRepository documentVersionRepository,
    IAuditService auditService, IAuditQueryService auditQueryService, ArchivePrincipal principal)
    : ObservableObject
{
    private Guid? _currentDocumentUid;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoSelection))]
    private bool _hasSelection;

    public bool NoSelection => !HasSelection;

    [ObservableProperty]
    private string _archiveNumber = "";

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _archiveDate = "";

    [ObservableProperty]
    private string _status = "";

    [ObservableProperty]
    private PreviewKind _kind = PreviewKind.None;

    [ObservableProperty]
    private string? _filePath;

    [ObservableProperty]
    private string? _fileSizeDisplay;

    [ObservableProperty]
    private string? _pageCountDisplay;

    [ObservableProperty]
    private string? _extractionStatusDisplay;

    [ObservableProperty]
    private string? _sha256;

    [ObservableProperty]
    private bool _versionsLoaded;

    [ObservableProperty]
    private bool _activityLoaded;

    public ObservableCollection<VersionRowViewModel> Versions { get; } = [];

    public ObservableCollection<ActivityRowViewModel> ActivityEntries { get; } = [];

    /// <summary>Called when the Explorer's selection changes to exactly one document. Clears preview for zero or multiple selections.</summary>
    public async Task ShowDocumentAsync(Guid? documentUid, CancellationToken cancellationToken)
    {
        if (documentUid == _currentDocumentUid)
        {
            return;
        }

        if (documentUid is not { } uid)
        {
            Clear();
            return;
        }

        var document = await documentRepository.GetByUidAsync(uid, cancellationToken);
        if (document is null)
        {
            Clear();
            return;
        }

        _currentDocumentUid = uid;
        VersionsLoaded = false;
        ActivityLoaded = false;
        Versions.Clear();
        ActivityEntries.Clear();

        HasSelection = true;
        ArchiveNumber = document.ArchiveNumber;
        Title = document.Title;
        ArchiveDate = document.ArchiveDate.ToString("yyyy-MM-dd");
        Status = document.Status.ToString();

        var version = document.CurrentVersionId is { } versionUid
            ? await documentVersionRepository.GetByUidAsync(versionUid, cancellationToken)
            : null;

        if (version is not null)
        {
            FilePath = version.FilePath;
            FileSizeDisplay = FileSizeFormatter.Format(version.FileSize);
            PageCountDisplay = version.PageCount?.ToString() ?? "—";
            ExtractionStatusDisplay = version.ContentExtractionStatus;
            Sha256 = version.Sha256;
            Kind = ClassifyPreviewKind(version.FileExtension);
        }
        else
        {
            FilePath = null;
            FileSizeDisplay = null;
            PageCountDisplay = null;
            ExtractionStatusDisplay = null;
            Sha256 = null;
            Kind = PreviewKind.None;
        }

        await auditService.RecordAsync(
            new AuditEntry(
                AuditAction.PreviewDocument, AuditActionCategory.DocumentView, AuditResult.Success,
                UserId: principal.UserId, EntityType: "document", EntityUid: uid.ToString(), EntityNameSnapshot: document.Title),
            cancellationToken);
    }

    public void Clear()
    {
        _currentDocumentUid = null;
        HasSelection = false;
        Versions.Clear();
        ActivityEntries.Clear();
        VersionsLoaded = false;
        ActivityLoaded = false;
    }

    [RelayCommand]
    private async Task LoadVersionsAsync()
    {
        if (VersionsLoaded || _currentDocumentUid is not { } uid)
        {
            return;
        }

        var versions = await documentVersionRepository.ListForDocumentAsync(uid, CancellationToken.None);
        Versions.Clear();
        foreach (var version in versions.OrderByDescending(v => v.VersionNo))
        {
            Versions.Add(new VersionRowViewModel(version));
        }

        VersionsLoaded = true;
    }

    [RelayCommand]
    private async Task LoadActivityAsync()
    {
        if (ActivityLoaded || _currentDocumentUid is not { } uid)
        {
            return;
        }

        var events = await auditQueryService.QueryAsync(new AuditQuery(EntityUid: uid.ToString(), Take: 50), CancellationToken.None);
        ActivityEntries.Clear();
        foreach (var auditEvent in events)
        {
            ActivityEntries.Add(new ActivityRowViewModel(auditEvent));
        }

        ActivityLoaded = true;
    }

    [RelayCommand]
    private void OpenExternally()
    {
        if (FilePath is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo(FilePath) { UseShellExecute = true });
    }

    private static PreviewKind ClassifyPreviewKind(string fileExtension) => fileExtension.ToLowerInvariant() switch
    {
        ".pdf" => PreviewKind.Pdf,
        ".docx" or ".xlsx" or ".pptx" or ".doc" or ".xls" or ".ppt" => PreviewKind.OfficeFallback,
        _ => PreviewKind.Unsupported,
    };
}
