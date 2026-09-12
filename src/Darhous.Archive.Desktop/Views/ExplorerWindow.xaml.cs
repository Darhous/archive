using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Darhous.Archive.Desktop.ViewModels.Explorer;
using Darhous.Archive.Desktop.ViewModels.Explorer.Preview;
using Darhous.Archive.Modules.Reports;
using Darhous.Archive.Modules.Reports.Printing;
using Darhous.Archive.Modules.Reports.SavedViews;
using Darhous.Archive.Security.Authentication;
using Darhous.Archive.Security.Sessions;

namespace Darhous.Archive.Desktop.Views;

public partial class ExplorerWindow : Window
{
    private readonly ExplorerViewModel _viewModel;
    private readonly string? _sessionToken;
    private readonly IAuthenticationService _authenticationService;
    private readonly ISavedViewReportService _savedViewReportService;
    private readonly IPdfPrintService _pdfPrintService;
    private readonly Action _onLogout;

    public ExplorerWindow(
        ExplorerViewModel viewModel, ArchivePrincipal principal, string? sessionToken,
        IAuthenticationService authenticationService,
        ISavedViewReportService savedViewReportService,
        IPdfPrintService pdfPrintService,
        Action onLogout)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _sessionToken = sessionToken;
        _authenticationService = authenticationService;
        _savedViewReportService = savedViewReportService;
        _pdfPrintService = pdfPrintService;
        _onLogout = onLogout;

        DataContext = viewModel;
        UserText.Text = principal.IsGuest ? "وضع الضيف (Guest Mode)" : $"{principal.DisplayName} — {principal.Role}";

        Loaded += async (_, _) => await viewModel.InitializeAsync();
        viewModel.StatusMessage += (_, message) => Title = $"Darhous Smart Archive — {message}";
        viewModel.Preview.PropertyChanged += Preview_PropertyChanged;
    }

    /// <summary>
    /// WebView2 navigation is driven imperatively rather than through XAML binding — its
    /// Source setter needs CoreWebView2 initialized first, and navigating on every unrelated
    /// property change (e.g. Status) would reload the same PDF repeatedly. Only FilePath/Kind
    /// actually changing to a PDF should trigger a navigation.
    /// </summary>
    private async void Preview_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(PreviewViewModel.FilePath) or nameof(PreviewViewModel.Kind)))
        {
            return;
        }

        if (sender is not PreviewViewModel preview || preview.Kind != PreviewKind.Pdf || preview.FilePath is null)
        {
            return;
        }

        await PdfPreview.EnsureCoreWebView2Async();
        PdfPreview.Source = new Uri(preview.FilePath);
    }

    private async void PreviewTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != PreviewTabs)
        {
            return; // ListView selection inside a tab also raises SelectionChanged — only react to the TabControl's own event.
        }

        if (PreviewTabs.SelectedItem == VersionsTab)
        {
            await _viewModel.Preview.LoadVersionsCommand.ExecuteAsync(null);
        }
        else if (PreviewTabs.SelectedItem == ActivityTab)
        {
            await _viewModel.Preview.LoadActivityCommand.ExecuteAsync(null);
        }
    }

    private void FolderTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FolderNodeViewModel node)
        {
            _viewModel.SelectedFolder = node;
        }
    }

    private async void DocumentListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        foreach (DocumentRowViewModel row in e.RemovedItems)
        {
            row.IsSelected = false;
        }

        foreach (DocumentRowViewModel row in e.AddedItems)
        {
            row.IsSelected = true;
        }

        _viewModel.UpdateSelectionCount();
        await _viewModel.UpdatePreviewForSelectionAsync();
    }

    private async void Logout_Click(object sender, RoutedEventArgs e)
    {
        if (_sessionToken is not null)
        {
            await _authenticationService.LogoutAsync(_sessionToken, CancellationToken.None);
        }

        _onLogout();
        Close();
    }

    private async void ExportReport_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string formatName } ||
            !Enum.TryParse<ReportFormat>(formatName, ignoreCase: true, out var format))
        {
            return;
        }

        var (extension, filter) = format switch
        {
            ReportFormat.Excel => (".xlsx", "Excel workbook (*.xlsx)|*.xlsx"),
            ReportFormat.Csv => (".csv", "CSV file (*.csv)|*.csv"),
            ReportFormat.Pdf => (".pdf", "PDF document (*.pdf)|*.pdf"),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = extension,
            Filter = filter,
            FileName = $"archive-view-{DateTime.Now:yyyyMMdd-HHmm}",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await _savedViewReportService.ExportAsync(
                _viewModel.CreateCurrentViewReport(),
                format,
                dialog.FileName,
                CancellationToken.None);
            Title = $"Darhous Smart Archive — تم تصدير التقرير إلى {dialog.FileName}";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "فشل تصدير التقرير", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void PrintReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var printDirectory = Path.Combine(Path.GetTempPath(), "Darhous.Archive", "PrintJobs");
            Directory.CreateDirectory(printDirectory);
            var pdfPath = Path.Combine(printDirectory, $"archive-view-{Guid.CreateVersion7():N}.pdf");
            await _savedViewReportService.ExportAsync(
                _viewModel.CreateCurrentViewReport(),
                ReportFormat.Pdf,
                pdfPath,
                CancellationToken.None);
            await _pdfPrintService.PrintAsync(pdfPath, CancellationToken.None);
            Title = "Darhous Smart Archive — تم إرسال التقرير إلى معالج الطباعة الافتراضي.";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "فشل طباعة التقرير", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
