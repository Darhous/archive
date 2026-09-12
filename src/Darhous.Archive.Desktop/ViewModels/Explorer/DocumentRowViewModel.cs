using CommunityToolkit.Mvvm.ComponentModel;
using Darhous.Archive.Application.Persistence;

namespace Darhous.Archive.Desktop.ViewModels.Explorer;

/// <summary>
/// One row in the document list (SAD §26 نتيجة البحث). Deliberately a thin, cheap-to-construct
/// wrapper — the 100k-row virtualization gate (Implementation Plan §41) depends on rows being
/// lightweight, since WPF still allocates a container per realized (visible) row even with
/// virtualization on.
/// </summary>
public sealed partial class DocumentRowViewModel(Document document) : ObservableObject
{
    public Guid Uid { get; } = document.Uid;

    public string ArchiveNumber { get; } = document.ArchiveNumber;

    public string Title { get; } = document.Title;

    public string Status { get; } = document.Status.ToString();

    public string ArchiveDate { get; } = document.ArchiveDate.ToString("yyyy-MM-dd");

    [ObservableProperty]
    private bool _isSelected;
}
