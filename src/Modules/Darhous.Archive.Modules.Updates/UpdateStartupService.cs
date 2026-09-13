using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Darhous.Archive.Modules.Updates;

internal sealed class UpdateStartupService(
    IUpdateSettingsService settingsService,
    IUpdateService updateService,
    IUpdateRequestService requestService,
    ILogger<UpdateStartupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        if (!settings.AutoCheck)
        {
            return;
        }

        try
        {
            var result = await updateService.CheckAsync(cancellationToken);
            if (result.IsUpdateAvailable && settings.AutoInstall && result.Latest is not null)
            {
                await requestService.QueueInstallAsync(result.Latest.PackagePath, null, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // An absent local feed is a normal offline state; malformed/inaccessible feeds are
            // logged but must not prevent the desktop from starting.
            logger.LogWarning(exception, "Automatic update check failed during startup");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
