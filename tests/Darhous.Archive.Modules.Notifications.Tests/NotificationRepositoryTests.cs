using System;
using System.Threading;
using System.Threading.Tasks;
using Darhous.Archive.Modules.Notifications;
using Darhous.Archive.Persistence.Connections;
using Microsoft.Data.Sqlite;
using Xunit;
using Dapper;

namespace Darhous.Archive.Modules.Notifications.Tests;

public class NotificationRepositoryTests
{
    // A simple in-memory connection factory for tests
    private class TestConnectionFactory : ISqliteConnectionFactory, IDisposable
    {
        private readonly SqliteConnection _masterConn;

        public TestConnectionFactory()
        {
            _masterConn = new SqliteConnection("Data Source=testdb;Mode=Memory;Cache=Shared");
            _masterConn.Open();
            // Need to create table for test
            _masterConn.Execute(
                """
                CREATE TABLE notifications (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    uid TEXT UNIQUE NOT NULL,
                    user_id INTEGER NULL,
                    type TEXT NOT NULL,
                    severity TEXT NOT NULL,
                    title TEXT NOT NULL,
                    body TEXT NOT NULL,
                    source TEXT NOT NULL,
                    action_json TEXT NULL,
                    created_at INTEGER NOT NULL,
                    read_at INTEGER NULL,
                    dismissed_at INTEGER NULL
                );
                """);
        }

        public Task<SqliteConnection> OpenAsync(Darhous.Archive.Persistence.Configuration.DatabaseKind database, CancellationToken cancellationToken)
        {
            var conn = new SqliteConnection("Data Source=testdb;Mode=Memory;Cache=Shared");
            conn.Open();
            return Task.FromResult(conn);
        }

        public void Dispose()
        {
            _masterConn.Dispose();
        }
    }

    [Fact]
    public async Task CreateAsync_ShouldInsertNotification()
    {
        using var factory = new TestConnectionFactory();
        var repo = new NotificationRepository(factory);

        var notification = new Notification(
            Id: 0,
            Uid: Guid.NewGuid(),
            UserId: null,
            Type: "test.type",
            Severity: "info",
            Title: "Test Title",
            Body: "Test Body",
            Source: "test",
            ActionJson: null,
            CreatedAt: DateTimeOffset.UtcNow,
            ReadAt: null,
            DismissedAt: null
        );

        var created = await repo.CreateAsync(notification, CancellationToken.None);

        Assert.True(created.Id > 0);
        Assert.Equal(notification.Uid, created.Uid);

        var list = await repo.ListAsync(unreadOnly: false, limit: 10, offset: 0, CancellationToken.None);
        Assert.Single(list);
    }
}
