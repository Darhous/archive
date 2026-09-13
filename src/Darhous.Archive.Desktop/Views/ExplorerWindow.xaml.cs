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
using Darhous.Archive.Core.Permissions;
using Darhous.Backup.Local;
using Darhous.Archive.Modules.Updates;

namespace Darhous.Archive.Desktop.Views;

public partial class ExplorerWindow : Window
{
    private readonly ExplorerViewModel _viewModel;
    private readonly string? _sessionToken;
    private readonly IAuthenticationService _authenticationService;
    private readonly ISavedViewReportService _savedViewReportService;
    private readonly IPdfPrintService _pdfPrintService;
    private readonly IBackupRequestService _backupRequestService;
    private readonly ILocalBackupService _localBackupService;
    private readonly IUpdateService _updateService;
    private readonly IUpdateSettingsService _updateSettingsService;
    private readonly IUpdateRequestService _updateRequestService;
    private readonly Func<AiSettingsWindow> _aiSettingsWindowFactory;
    private readonly ArchivePrincipal _principal;
    private readonly Action _onLogout;

    public ExplorerWindow(
        ExplorerViewModel viewModel, ArchivePrincipal principal, string? sessionToken,
        IAuthenticationService authenticationService,
        ISavedViewReportService savedViewReportService,
        IPdfPrintService pdfPrintService,
        IBackupRequestService backupRequestService,
        ILocalBackupService localBackupService,
        IUpdateService updateService,
        IUpdateSettingsService updateSettingsService,
        IUpdateRequestService updateRequestService,
        Func<AiSettingsWindow> aiSettingsWindowFactory,
        Action onLogout)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _sessionToken = sessionToken;
        _authenticationService = authenticationService;
        _savedViewReportService = savedViewReportService;
        _pdfPrintService = pdfPrintService;
        _backupRequestService = backupRequestService;
        _localBackupService = localBackupService;
        _updateService = updateService;
        _updateSettingsService = updateSettingsService;
        _updateRequestService = updateRequestService;
        _aiSettingsWindowFactory = aiSettingsWindowFactory;
        _principal = principal;
        _onLogout = onLogout;

        DataContext = viewModel;
        UserText.Text = principal.IsGuest ? "وضع الضيف (Guest Mode)" : $"{principal.DisplayName} — {principal.Role}";
        BackupMenu.IsEnabled = principal.Role == UserRole.Admin;
        UpdatesMenu.IsEnabled = principal.Role == UserRole.Admin;
        AiSettingsMenu.IsEnabled = principal.Role == UserRole.Admin;

        Loaded += async (_, _) => await viewModel.InitializeAsync();
        viewModel.StatusMessage += (_, message) => Title = $"Darhous Smart Archive — {message}";
        viewModel.Preview.PropertyChanged += Preview_PropertyChanged;
    }

    private void AiSettings_Click(object sender, RoutedEventArgs e)
    {
        var window = _aiSettingsWindowFactory();
        window.Owner = this;
        window.ShowDialog();
    }

    private void Updates_Click(object sender, RoutedEventArgs e)
    {
        var window = new UpdateSettingsWindow(
            _updateService,
            _updateSettingsService,
            _updateRequestService,
            _principal.UserId)
        {
            Owner = this,
        };
        window.ShowDialog();
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

    private async void QueueBackup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string typeName } ||
            !Enum.TryParse<BackupType>(typeName, ignoreCase: true, out var type))
        {
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = "اختر مجلد وجهة النسخة الاحتياطية",
            Multiselect = false,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var jobUid = await _backupRequestService.QueueAsync(
                new BackupRequest(type, dialog.FolderName, _principal.UserId),
                CancellationToken.None);
            Title = $"Darhous Smart Archive — تمت جدولة النسخة الاحتياطية ({jobUid}).";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "فشل جدولة النسخة الاحتياطية", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "اختر حزمة النسخة الاحتياطية",
            Filter = "Darhous backup (*.darhousbackup)|*.darhousbackup|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            "ستُفحص الحزمة أولًا، ثم تُنشأ نسخة أمان كاملة من الحالة الحالية قبل الاستعادة. سيُغلق التطبيق بعد النجاح. هل تريد المتابعة؟",
            "تأكيد استعادة النسخة الاحتياطية",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        BackupMenu.IsEnabled = false;
        try
        {
            var result = await _localBackupService.RestoreAsync(dialog.FileName, _principal.UserId, CancellationToken.None);
            MessageBox.Show(
                this,
                $"اكتملت الاستعادة والتحقق. حُفظت نسخة الأمان الحالية هنا:\n{result.SafetyBackupPath}\n\nسيُغلق التطبيق الآن لإعادة تحميل الحالة المستعادة بأمان.",
                "اكتملت الاستعادة",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            System.Windows.Application.Current.Shutdown();
        }
        catch (RestoreRecoveryRequiredException exception)
        {
            MessageBox.Show(
                this,
                $"فشلت الاستعادة بعد بدء تغيير الحالة الحية. يجب إغلاق التطبيق وعدم متابعة العمل. نسخة الأمان:\n{exception.SafetyBackupPath}\n\n{exception.Message}",
                "الاستعادة تحتاج تدخلاً",
                MessageBoxButton.OK,
                MessageBoxImage.Stop);
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception exception)
        {
            BackupMenu.IsEnabled = true;
            MessageBox.Show(this, exception.Message, "فشل استعادة النسخة الاحتياطية", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
