using Darhous.Archive.Configuration;

namespace Darhous.Archive.Modules.Updates;

internal sealed class UpdateSettingsService(IAppSettingsStore settingsStore) : IUpdateSettingsService
{
    internal const string AutoCheckKey = "updates.auto_check";
    internal const string AutoInstallKey = "updates.auto_install";

    public async Task<UpdateUserSettings> GetAsync(CancellationToken cancellationToken)
    {
        var autoCheck = await settingsStore.GetAsync(AutoCheckKey, cancellationToken);
        var autoInstall = await settingsStore.GetAsync(AutoInstallKey, cancellationToken);
        return new UpdateUserSettings(Parse(autoCheck, defaultValue: true), Parse(autoInstall, defaultValue: false));
    }

    public async Task SaveAsync(UpdateUserSettings settings, CancellationToken cancellationToken)
    {
        await settingsStore.SetAsync(AutoCheckKey, settings.AutoCheck ? "true" : "false", cancellationToken);
        await settingsStore.SetAsync(AutoInstallKey, settings.AutoInstall ? "true" : "false", cancellationToken);
    }

    private static bool Parse(string? value, bool defaultValue) =>
        value is null ? defaultValue : bool.TryParse(value, out var parsed) ? parsed : defaultValue;
}
