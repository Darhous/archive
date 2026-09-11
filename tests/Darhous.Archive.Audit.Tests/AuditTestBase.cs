using Darhous.Archive.Audit.Writing;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Persistence;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Writes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darhous.Archive.Audit.Tests;

/// <summary>Same isolated-temp-directory pattern as Persistence.Tests/Security.Tests.</summary>
public abstract class AuditTestBase : IAsyncLifetime
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), "darhous-audit-tests", Guid.NewGuid().ToString("N"));

    private SqliteWriteQueue? _writeQueue;

    protected PersistenceOptions Options { get; private set; } = null!;
    protected BufferedAuditService AuditService { get; private set; } = null!;
    protected ISqliteConnectionFactory ConnectionFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_tempDirectory);
        Options = new PersistenceOptions { DataDirectory = _tempDirectory };
        await PersistenceInitializer.InitializeAsync(Options, CancellationToken.None);

        ConnectionFactory = new SqliteConnectionFactory(Options);
        _writeQueue = new SqliteWriteQueue(DatabaseKind.Audit, ConnectionFactory, NullLogger<SqliteWriteQueue>.Instance);
        await _writeQueue.StartAsync(CancellationToken.None);

        AuditService = new BufferedAuditService(_writeQueue, ConnectionFactory, new SystemClock(), NullLogger<BufferedAuditService>.Instance);
        await AuditService.StartAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        if (AuditService is not null)
        {
            await AuditService.StopAsync(CancellationToken.None);
        }

        if (_writeQueue is not null)
        {
            await _writeQueue.StopAsync(CancellationToken.None);
        }

        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    protected async Task<long> CountAuditEventsAsync()
    {
        await using var connection = await ConnectionFactory.OpenAsync(DatabaseKind.Audit, CancellationToken.None);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM audit_events;";
        return (long)(await cmd.ExecuteScalarAsync())!;
    }
}
