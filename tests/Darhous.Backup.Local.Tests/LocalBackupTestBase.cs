using Darhous.Archive.Core.Time;
using Darhous.Archive.Persistence;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Writes;
using Darhous.Backup.Local.Packaging;
using Darhous.Search.SqliteFts.Rebuild;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darhous.Backup.Local.Tests;

public abstract class LocalBackupTestBase : IAsyncLifetime
{
    private readonly List<SqliteWriteQueue> _queues = [];

    protected string Root { get; private set; } = null!;
    protected string Destination { get; private set; } = null!;
    protected string ManagedFiles { get; private set; } = null!;
    protected string Settings { get; private set; } = null!;
    protected string Safety { get; private set; } = null!;
    protected PersistenceOptions PersistenceOptions { get; private set; } = null!;
    protected ISqliteConnectionFactory ConnectionFactory { get; private set; } = null!;
    protected ILocalBackupService Service { get; private set; } = null!;
    protected RecordingSearchRebuilder Rebuilder { get; } = new();

    public async Task InitializeAsync()
    {
        Root = Path.Combine(Path.GetTempPath(), "Darhous.Backup.Tests", Guid.NewGuid().ToString("N"));
        Destination = Path.Combine(Root, "destination");
        ManagedFiles = Path.Combine(Root, "managed");
        Settings = Path.Combine(Root, "settings");
        Safety = Path.Combine(Root, "safety");
        var working = Path.Combine(Root, "working");
        Directory.CreateDirectory(Destination);
        Directory.CreateDirectory(ManagedFiles);
        Directory.CreateDirectory(Settings);
        Directory.CreateDirectory(Safety);
        Directory.CreateDirectory(working);

        PersistenceOptions = new PersistenceOptions { DataDirectory = Path.Combine(Root, "data") };
        await PersistenceInitializer.InitializeAsync(PersistenceOptions, CancellationToken.None);
        ConnectionFactory = new SqliteConnectionFactory(PersistenceOptions);

        foreach (var database in Enum.GetValues<DatabaseKind>())
        {
            var queue = new SqliteWriteQueue(database, ConnectionFactory, NullLogger<SqliteWriteQueue>.Instance);
            await queue.StartAsync(CancellationToken.None);
            _queues.Add(queue);
        }

        var archive = _queues[(int)DatabaseKind.Archive];
        var history = new BackupHistoryStore(archive, new SystemClock());
        Service = new LocalBackupService(
            new LocalBackupOptions
            {
                ManagedFilesDirectory = ManagedFiles,
                SettingsDirectory = Settings,
                SafetyBackupDirectory = Safety,
                WorkingDirectory = working,
                ApplicationVersion = new Version(1, 0, 0),
            },
            PersistenceOptions,
            ConnectionFactory,
            history,
            new BackupPackageValidator(),
            Rebuilder,
            archive,
            _queues[(int)DatabaseKind.Audit],
            _queues[(int)DatabaseKind.Search],
            new SystemClock(),
            NullLogger<LocalBackupService>.Instance);
    }

    public async Task DisposeAsync()
    {
        foreach (var queue in _queues.AsEnumerable().Reverse())
        {
            await queue.StopAsync(CancellationToken.None);
            queue.Dispose();
        }

        SqliteConnection.ClearAllPools();
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
    }

    protected SqliteWriteQueue Queue(DatabaseKind database) => _queues[(int)database];

    protected Task SetArchiveSettingAsync(string key, string value) =>
        Queue(DatabaseKind.Archive).EnqueueAsync(async (connection, transaction, ct) =>
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "INSERT INTO app_settings (setting_key, value_json, updated_at) VALUES (@key, @value, 1) " +
                "ON CONFLICT(setting_key) DO UPDATE SET value_json = excluded.value_json;";
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@value", value);
            await command.ExecuteNonQueryAsync(ct);
            return true;
        }, CancellationToken.None);

    protected async Task<string?> ReadArchiveSettingAsync(string key)
    {
        await using var connection = await ConnectionFactory.OpenAsync(DatabaseKind.Archive, CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value_json FROM app_settings WHERE setting_key = @key;";
        command.Parameters.AddWithValue("@key", key);
        return (string?)await command.ExecuteScalarAsync();
    }

    protected sealed class RecordingSearchRebuilder : ISearchIndexRebuilder
    {
        public int Calls { get; private set; }

        public Task RebuildAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }
}
