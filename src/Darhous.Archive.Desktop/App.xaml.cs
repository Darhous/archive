using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Darhous.Archive.Application;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Audit;
using Darhous.Archive.Configuration;
using Darhous.Archive.Core.Hosting;
using Darhous.Archive.Modules.Discovery;
using Darhous.Archive.Modules.Discovery.Exclusions;
using Darhous.Archive.Modules.Discovery.WatchFolders;
using Darhous.Archive.Modules.Documents;
using Darhous.Archive.Modules.Folders;
using Darhous.Archive.Modules.Importers;
using Darhous.Archive.Modules.Plugins;
using Darhous.Archive.Modules.Reports;
using Darhous.Archive.Modules.Reports.Printing;
using Darhous.Archive.Modules.Reports.SavedViews;
using Darhous.Archive.Core.Audit;
using Darhous.Archive.Core.Events;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Modules.Documents.Services;
using Darhous.Archive.Desktop.Hosting;
using Darhous.Archive.Desktop.Themes;
using Darhous.Archive.Desktop.ViewModels;
using Darhous.Archive.Desktop.ViewModels.Explorer;
using Darhous.Archive.Desktop.ViewModels.Explorer.Preview;
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
        builder.Services.AddDiscoveryModule();
        builder.Services.AddImportersModule();
        builder.Services.AddReportsModule();
        builder.Services.AddJobRunner();
        builder.Services.AddPluginsModule(
            typeof(App).Assembly.GetName().Version ?? new Version(1, 0, 0),
            exposeHostServices: registry =>
            {
                // Plugin SDK §23 "الخدمات الرسمية" — only the official services that exist
                // today; the rest of that list (ITagService/ISearchService/IUserContext/
                // IJobService/INotificationService/ISettingsService/IHealthService) is wired
                // in here once those modules exist in later phases.
                var services = _host!.Services;
                registry.AddSingleton(services.GetRequiredService<IDocumentService>());
                registry.AddSingleton(services.GetRequiredService<IFolderService>());
                registry.AddSingleton(services.GetRequiredService<IAuditService>());
                registry.AddSingleton(services.GetRequiredService<IEventBus>());
                registry.AddSingleton(services.GetRequiredService<IClock>());
            });
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

        await _host.Services.GetRequiredService<IExclusionService>().SeedTechnicalExclusionsAsync(CancellationToken.None);

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

        viewModel.LoginSucceeded += async (_, args) =>
        {
            await MaybeShowOnboardingAsync(args.Principal);

            var previewViewModel = new PreviewViewModel(
                _host.Services.GetRequiredService<IDocumentRepository>(),
                _host.Services.GetRequiredService<IDocumentVersionRepository>(),
                _host.Services.GetRequiredService<Core.Audit.IAuditService>(),
                _host.Services.GetRequiredService<Core.Audit.IAuditQueryService>(),
                args.Principal);

            var explorerViewModel = new ExplorerViewModel(
                _host.Services.GetRequiredService<Modules.Folders.IFolderService>(),
                _host.Services.GetRequiredService<IDocumentRepository>(),
                _host.Services.GetRequiredService<Modules.Documents.Services.IDocumentService>(),
                _host.Services.GetRequiredService<Modules.Documents.BulkOperations.IBulkOperationService>(),
                previewViewModel,
                args.Principal);

            var explorer = new ExplorerWindow(
                explorerViewModel, args.Principal, args.SessionToken,
                _host.Services.GetRequiredService<IAuthenticationService>(),
                _host.Services.GetRequiredService<ISavedViewReportService>(),
                _host.Services.GetRequiredService<IPdfPrintService>(),
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

    /// <summary>
    /// SAD §46.9 — shown once, after the first successful login, until the user makes an
    /// explicit choice (start scanning specific folders, scan the whole computer, or skip).
    /// Tracked via <see cref="IAppSettingsStore"/> rather than re-checking "are there any
    /// watch folders yet" so an explicit Skip doesn't re-prompt on every subsequent login.
    /// </summary>
    private async Task MaybeShowOnboardingAsync(ArchivePrincipal principal)
    {
        var settingsStore = _host!.Services.GetRequiredService<IAppSettingsStore>();
        var alreadyShown = await settingsStore.GetAsync("onboarding_completed", CancellationToken.None);
        if (alreadyShown == "true")
        {
            return;
        }

        var onboardingViewModel = new OnboardingViewModel(
            _host.Services.GetRequiredService<IWatchFolderService>(),
            _host.Services.GetRequiredService<IDiscoveryOrchestrator>(),
            principal);

        var onboardingWindow = new OnboardingWindow(onboardingViewModel);
        onboardingWindow.ShowDialog();

        await settingsStore.SetAsync("onboarding_completed", "true", CancellationToken.None);
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
