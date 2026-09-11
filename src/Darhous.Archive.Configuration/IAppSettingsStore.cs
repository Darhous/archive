namespace Darhous.Archive.Configuration;

/// <summary>
/// Key/value settings store matching the shape of the `app_settings` table (DB Spec §71).
/// The real SQLite-backed implementation is added with Persistence in Phase 2; until then
/// <see cref="InMemoryAppSettingsStore"/> lets everything above Configuration compile and
/// be tested against a working (if non-persistent) implementation.
/// </summary>
public interface IAppSettingsStore
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken);

    Task SetAsync(string key, string value, CancellationToken cancellationToken);
}
