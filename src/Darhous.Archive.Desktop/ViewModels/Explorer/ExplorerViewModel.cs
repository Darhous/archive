using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Core.Text;
using Darhous.Archive.Modules.Documents.BulkOperations;
using Darhous.Archive.Modules.Documents.Services;
using Darhous.Archive.Modules.Folders;
using Darhous.Archive.Security.Sessions;

namespace Darhous.Archive.Desktop.ViewModels.Explorer;

/// <summary>
/// SAD §37 (Archive Explorer). Live filtering is a client-side substring match on the
/// currently-loaded scope for now — real full-text search/ranking/pagination against
/// search.db is Phase 8; this proves the UI shell (tree, list, breadcrumb, bulk actions,
/// virtualization) works before Search exists to plug into it.
/// </summary>
public sealed partial class ExplorerViewModel : ObservableObject
{
    private readonly IFolderService _folderService;
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentService _documentService;
    private readonly IBulkOperationService _bulkOperationService;
    private readonly ArchivePrincipal _principal;

    private List<DocumentRowViewModel> _currentScopeDocuments = [];
    private Dictionary<Guid, string> _folderPathByUid = [];

    public event EventHandler<string>? StatusMessage;

    public ObservableCollection<FolderNodeViewModel> RootNodes { get; } = [];

    public ObservableCollection<DocumentRowViewModel> Documents { get; } = [];

    public IReadOnlyList<DensityMode> DensityOptions { get; } = Enum.GetValues<DensityMode>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private FolderNodeViewModel? _selectedFolder;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _breadcrumb = "كل الأرشيف";

    [ObservableProperty]
    private bool _showPreviewPane = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowPadding))]
    private DensityMode _density = DensityMode.Standard;

    /// <summary>UI/UX §93 Density Modes, applied to each document-list row.</summary>
    public System.Windows.Thickness RowPadding => Density switch
    {
        DensityMode.Compact => new System.Windows.Thickness(4, 2, 4, 2),
        DensityMode.Comfortable => new System.Windows.Thickness(8, 10, 8, 10),
        _ => new System.Windows.Thickness(6, 6, 6, 6),
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private int _selectedDocumentCount;

    [ObservableProperty]
    private FolderNodeViewModel? _moveTargetFolder;

    public bool HasSelection => SelectedDocumentCount > 0;

    public ExplorerViewModel(
        IFolderService folderService, IDocumentRepository documentRepository, IDocumentService documentService,
        IBulkOperationService bulkOperationService, ArchivePrincipal principal)
    {
        _folderService = folderService;
        _documentRepository = documentRepository;
        _documentService = documentService;
        _bulkOperationService = bulkOperationService;
        _principal = principal;
    }

    public async Task InitializeAsync()
    {
        await RebuildFolderTreeAsync();

        var allArchive = new FolderNodeViewModel(null, "كل الأرشيف") { IsSelected = true };
        RootNodes.Insert(0, allArchive);
        SelectedFolder = allArchive;

        await LoadDocumentsForSelectedFolderAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    async partial void OnSelectedFolderChanged(FolderNodeViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        Breadcrumb = BuildBreadcrumb(value);
        await LoadDocumentsForSelectedFolderAsync();
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var selected = Documents.Where(d => d.IsSelected).ToList();
        foreach (var row in selected)
        {
            await _documentService.TrashDocumentAsync(row.Uid, _principal.UserId, reason: null, CancellationToken.None);
        }

        await LoadDocumentsForSelectedFolderAsync();
        StatusMessage?.Invoke(this, $"تم نقل {selected.Count} مستند إلى سلة المحذوفات.");
    }

    [RelayCommand]
    private async Task MoveSelectedToFolderAsync()
    {
        var selectedUids = Documents.Where(d => d.IsSelected).Select(d => d.Uid).ToList();
        if (selectedUids.Count == 0)
        {
            return;
        }

        var result = await _bulkOperationService.BulkMoveDocumentsAsync(
            selectedUids, MoveTargetFolder?.Uid, _principal.UserId, CancellationToken.None);

        await LoadDocumentsForSelectedFolderAsync();

        StatusMessage?.Invoke(this, result.IsSuccess
            ? $"تم نقل {selectedUids.Count} مستند. (Undo متاح خلال 30 ثانية — Snapshot: {result.Value})"
            : $"فشل النقل: {result.Error!.Message}");
    }

    public void UpdateSelectionCount() => SelectedDocumentCount = Documents.Count(d => d.IsSelected);

    private async Task RebuildFolderTreeAsync()
    {
        RootNodes.Clear();

        var all = await _folderService.GetChildrenAsync(null, CancellationToken.None);
        // GetChildrenAsync only returns direct children — build the full tree by walking down.
        foreach (var folder in all)
        {
            var node = new FolderNodeViewModel(folder.Uid, folder.Name);
            await PopulateChildrenAsync(node);
            RootNodes.Add(node);
        }

        RootNodes.Add(new FolderNodeViewModel(null, "غير مصنف"));

        _folderPathByUid = BuildPathIndex(RootNodes, "");
    }

    private async Task PopulateChildrenAsync(FolderNodeViewModel node)
    {
        if (node.Uid is null)
        {
            return;
        }

        var children = await _folderService.GetChildrenAsync(node.Uid, CancellationToken.None);
        foreach (var child in children)
        {
            var childNode = new FolderNodeViewModel(child.Uid, child.Name);
            await PopulateChildrenAsync(childNode);
            node.Children.Add(childNode);
        }
    }

    private static Dictionary<Guid, string> BuildPathIndex(IEnumerable<FolderNodeViewModel> nodes, string prefix)
    {
        var result = new Dictionary<Guid, string>();
        foreach (var node in nodes)
        {
            if (node.Uid is { } uid)
            {
                var path = prefix.Length == 0 ? node.Name : $"{prefix} > {node.Name}";
                result[uid] = path;
                foreach (var (childUid, childPath) in BuildPathIndex(node.Children, path))
                {
                    result[childUid] = childPath;
                }
            }
        }

        return result;
    }

    private string BuildBreadcrumb(FolderNodeViewModel folder) =>
        folder.Uid is { } uid && _folderPathByUid.TryGetValue(uid, out var path)
            ? $"كل الأرشيف > {path}"
            : folder.Name;

    private async Task LoadDocumentsForSelectedFolderAsync()
    {
        IReadOnlyList<Document> documents = SelectedFolder switch
        {
            { Uid: { } uid } => await _documentRepository.ListByFolderAsync(uid, CancellationToken.None),
            { Uid: null, Name: "غير مصنف" } => await _documentRepository.ListByFolderAsync(null, CancellationToken.None),
            _ => await _documentRepository.ListAsync(CancellationToken.None), // "كل الأرشيف" — real pagination lands in Phase 8
        };

        _currentScopeDocuments = documents.Select(d => new DocumentRowViewModel(d)).ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Documents.Clear();

        var normalizedQuery = ArabicNormalization.Normalize(SearchText);
        var matches = string.IsNullOrWhiteSpace(normalizedQuery)
            ? _currentScopeDocuments
            : _currentScopeDocuments.Where(d => ArabicNormalization.Normalize(d.Title).Contains(normalizedQuery, StringComparison.Ordinal));

        foreach (var row in matches)
        {
            Documents.Add(row);
        }
    }
}
