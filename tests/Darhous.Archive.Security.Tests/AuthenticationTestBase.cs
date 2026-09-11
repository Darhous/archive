using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Core.Permissions;
using Darhous.Archive.Core.Time;
using Darhous.Archive.Persistence;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Darhous.Archive.Persistence.Transactions;
using Darhous.Archive.Persistence.Writes;
using Darhous.Archive.Security.Authentication;
using Darhous.Archive.Security.Passwords;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darhous.Archive.Security.Tests;

/// <summary>
/// Same isolated-temp-directory pattern as Darhous.Archive.Persistence.Tests — Authentication
/// only makes sense tested against the real SQLite plumbing (hashing + DB round-trip +
/// session creation together), not mocked out.
/// </summary>
public abstract class AuthenticationTestBase : IAsyncLifetime
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), "darhous-auth-tests", Guid.NewGuid().ToString("N"));

    private SqliteWriteQueue? _writeQueue;

    protected PersistenceOptions Options { get; private set; } = null!;
    protected IUnitOfWork UnitOfWork { get; private set; } = null!;
    protected IPasswordHasher PasswordHasher { get; } =
        new Argon2idPasswordHasher(new Argon2idOptions(MemorySizeKiB: 8 * 1024, Iterations: 2, DegreeOfParallelism: 1));
    protected IClock Clock { get; } = new SystemClock();
    protected AuthenticationService AuthenticationService { get; private set; } = null!;
    protected UserManagementService UserManagementService { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_tempDirectory);
        Options = new PersistenceOptions { DataDirectory = _tempDirectory };
        await PersistenceInitializer.InitializeAsync(Options, CancellationToken.None);

        _writeQueue = new SqliteWriteQueue(DatabaseKind.Archive, new SqliteConnectionFactory(Options), NullLogger<SqliteWriteQueue>.Instance);
        await _writeQueue.StartAsync(CancellationToken.None);

        UnitOfWork = new SqliteUnitOfWork(_writeQueue);
        AuthenticationService = new AuthenticationService(UnitOfWork, PasswordHasher, Clock);
        UserManagementService = new UserManagementService(UnitOfWork, PasswordHasher);
    }

    public async Task DisposeAsync()
    {
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

    protected Task<Guid> CreateUserAsync(string username, string password, UserRole role = UserRole.User) =>
        UnitOfWork.ExecuteAsync(
            (context, ct) => context.Users.CreateAsync(username, username, PasswordHasher.Hash(password), "argon2id", role, false, ct),
            CancellationToken.None);
}
