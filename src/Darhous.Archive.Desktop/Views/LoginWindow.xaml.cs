using System.Windows;
using System.Windows.Input;
using Darhous.Archive.Desktop.ViewModels;

namespace Darhous.Archive.Desktop.Views;

public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not LoginViewModel viewModel)
        {
            return;
        }

        if (viewModel.LoginCommand.CanExecute(PasswordBox.Password))
        {
            viewModel.LoginCommand.Execute(PasswordBox.Password);
        }
    }
}
