using System.Windows;
using Darhous.Archive.Desktop.ViewModels;
using Microsoft.Win32;

namespace Darhous.Archive.Desktop.Views;

public partial class OnboardingWindow : Window
{
    public OnboardingWindow(OnboardingViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Completed += (_, _) => Close();
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "اختر مجلدًا للأرشفة" };
        if (dialog.ShowDialog(this) == true && DataContext is OnboardingViewModel viewModel)
        {
            viewModel.AddFolder(dialog.FolderName);
        }
    }

    private void ScanFullComputer_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not OnboardingViewModel viewModel)
        {
            return;
        }

        var confirmed = MessageBox.Show(
            this,
            "سيتم فحص كل الأقراص الثابتة المحلية بالكامل (باستثناء مجلدات النظام). قد يستغرق هذا وقتًا طويلاً حسب حجم البيانات. هل تريد المتابعة؟",
            "فحص الكمبيوتر بالكامل",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;

        if (confirmed && viewModel.ScanFullComputerCommand.CanExecute(null))
        {
            viewModel.ScanFullComputerCommand.Execute(null);
        }
    }
}
