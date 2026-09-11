using Darhous.Archive.Persistence.Configuration;

namespace Darhous.Archive.Persistence.Tests;

/// <summary>
/// Base class giving each test class its own isolated temp directory + fully migrated
/// 3-database set (xUnit creates one instance per test method, so each test gets fresh
/// files) — never touches the real %ProgramData% location.
/// </summary>
public abstract class PersistenceTestBase : IAsyncLifetime
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), "darhous-persistence-tests", Guid.NewGuid().ToString("N"));

    protected PersistenceOptions Options { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_tempDirectory);
        Options = new PersistenceOptions { DataDirectory = _tempDirectory };
        await PersistenceInitializer.InitializeAsync(Options, CancellationToken.None);
    }

    public Task DisposeAsync()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; a lingering SQLite handle on Windows shouldn't fail the test run.
        }

        return Task.CompletedTask;
    }
}
