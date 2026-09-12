using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Darhous.Archive.Application;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Audit;
using Darhous.Archive.Configuration;
using Darhous.Archive.Core.Hosting;
using Darhous.Archive.Modules.Documents;
using Darhous.Archive.Modules.Folders;
using Darhous.Archive.Desktop.Hosting;
using Darhous.Archive.Desktop.Themes;
using Darhous.Archive.Desktop.ViewModels;
using Darhous.Archive.Desktop.ViewModels.Explorer;
using Darhous.Archive.Desktop.Views;
using Darhous.Archive.Persistence;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Security;
using Darhous.Archive.Security.Authentication;
using Darhous.Archive.Security.Sessions;
using Darhous.Search.SqliteFts;

namespace Darhous.Archive.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var builder = ArchiveHostDefaults.CreateBuilder("Desktop", e.Args);
        builder.Services.AddApplicationLayer();
        builder.Services.AddPersistence();
        builder.Services.AddAudit();
        builder.Services.AddSecurity();
        builder.Services.AddDocumentsModule();
        builder.Services.AddFoldersModule();
        builder.Services.AddSearchPlugin();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<LoginWindow>();

        _host = builder.Build();

        var persistenceOptions = _host.Services.GetRequiredService<PersistenceOptions>();
        await PersistenceInitializer.InitializeAsync(persistenceOptions, CancellationToken.None);
        await _host.StartAsync();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();

        var generatedAdminPassword = await FirstRunBootstrapper.EnsureAdminExistsAsync(
            _host.Services.GetRequiredService<IUnitOfWork>(),
            _host.Services.GetRequiredService<IUserManagementService>(),
            CancellationToken.None);

        if (generatedAdminPassword is not null)
        {
            logger.LogInformation("First run: created default Admin account.");
            MessageBox.Show(
                $"تم إنشاء حساب Admin افتراضي لأول تشغيل:\n\nاسم المستخدم: admin\nكلمة المرور: {generatedAdminPassword}\n\n" +
                "سيُطلب منك تغييرها بعد أول دخول. احتفظ بها الآن — لن تُعرض مرة أخرى.",
                "Darhous Smart Archive — أول تشغيل",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        ThemeManager.Apply(AppTheme.System);
        ShowLoginWindow();
    }

    private void ShowLoginWindow()
    {
        var loginWindow = _host!.Services.GetRequiredService<LoginWindow>();
        var viewModel = (LoginViewModel)loginWindow.DataContext;

        viewModel.LoginSucceeded += (_, args) =>
        {
            var explorerViewModel = new ExplorerViewModel(
                _host.Services.GetRequiredService<Modules.Folders.IFolderService>(),
                _host.Services.GetRequiredService<IDocumentRepository>(),
                _host.Services.GetRequiredService<Modules.Documents.Services.IDocumentService>(),
                _host.Services.GetRequiredService<Modules.Documents.BulkOperations.IBulkOperationService>(),
                args.Principal);

            var explorer = new ExplorerWindow(
                explorerViewModel, args.Principal, args.SessionToken,
                _host.Services.GetRequiredService<IAuthenticationService>(),
                onLogout: ShowLoginWindow);

            explorer.Show();
            loginWindow.Close();
        };

        loginWindow.Closed += (_, _) =>
        {
            // Closed without logging in (e.g. the user clicked the window's X button) — only
            // shut down if no other top-level window (the Explorer) is taking over.
            if (Windows.Count == 0)
            {
                Shutdown();
            }
        };

        loginWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
