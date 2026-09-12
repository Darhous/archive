using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Darhous.Archive.Desktop.ViewModels.Explorer;

/// <summary>
/// One node in the sidebar folder tree. <see cref="Uid"/> is null for the two synthetic
/// nodes SAD §20 calls for: "كل الأرشيف" (root — not a real folder) and "غير مصنف"
/// (Unclassified — documents.folder_id IS NULL, not a real folder either).
/// </summary>
public sealed partial class FolderNodeViewModel(Guid? uid, string name) : ObservableObject
{
    public Guid? Uid { get; } = uid;

    public string Name { get; } = name;

    public ObservableCollection<FolderNodeViewModel> Children { get; } = [];

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;
}
