using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;

namespace Darhous.Archive.Modules.Notifications;

public sealed class NotificationRepository : INotificationRepository
{
    private const string SelectColumns =
        """
        SELECT id, uid, user_id, type, severity, title, body, source, action_json, created_at, read_at, dismissed_at
        FROM notifications
        """;

    private readonly ISqliteConnectionFactory? _connectionFactory;
    private readonly IDbConnection? _boundConnection;
    private readonly IDbTransaction? _boundTransaction;

    public NotificationRepository(ISqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public NotificationRepository(IDbConnection connection, IDbTransaction transaction)
    {
        _boundConnection = connection;
        _boundTransaction = transaction;
    }

    private async Task<IDbConnection> GetConnectionAsync(CancellationToken cancellationToken) => 
        _boundConnection ?? await _connectionFactory!.OpenAsync(DatabaseKind.Archive, cancellationToken);
        
    private IDbTransaction? GetTransaction() => _boundTransaction;

    public async Task<Notification> CreateAsync(Notification notification, CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var ownsConnection = _boundConnection == null;

        try
        {
            var command = new CommandDefinition(
                """
                INSERT INTO notifications (uid, user_id, type, severity, title, body, source, action_json, created_at, read_at, dismissed_at)
                VALUES (@Uid, @UserId, @Type, @Severity, @Title, @Body, @Source, @ActionJson, @CreatedAt, @ReadAt, @DismissedAt)
                RETURNING id;
                """,
                new
                {
                    Uid = notification.Uid.ToString(),
                    UserId = notification.UserId,
                    Type = notification.Type,
                    Severity = notification.Severity,
                    Title = notification.Title,
                    Body = notification.Body,
                    Source = notification.Source,
                    ActionJson = notification.ActionJson,
                    CreatedAt = notification.CreatedAt.ToUnixTimeMilliseconds(),
                    ReadAt = notification.ReadAt?.ToUnixTimeMilliseconds(),
                    DismissedAt = notification.DismissedAt?.ToUnixTimeMilliseconds()
                },
                transaction: GetTransaction(),
                cancellationToken: cancellationToken);

            var id = await connection.ExecuteScalarAsync<int>(command);
            return notification with { Id = id };
        }
        finally
        {
            if (ownsConnection)
                connection.Dispose();
        }
    }

    public async Task<IReadOnlyList<Notification>> ListAsync(bool unreadOnly, int limit, int offset, CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var ownsConnection = _boundConnection == null;

        try
        {
            var whereClause = unreadOnly ? "WHERE read_at IS NULL AND dismissed_at IS NULL" : "";

            var command = new CommandDefinition(
                $"""
                {SelectColumns}
                {whereClause}
                ORDER BY created_at DESC
                LIMIT @Limit OFFSET @Offset;
                """,
                new { Limit = limit, Offset = offset },
                transaction: GetTransaction(),
                cancellationToken: cancellationToken);

            var rows = await connection.QueryAsync<dynamic>(command);
            var result = new List<Notification>();

            foreach (var row in rows)
            {
                result.Add(MapRow(row));
            }

            return result;
        }
        finally
        {
            if (ownsConnection)
                connection.Dispose();
        }
    }

    public async Task MarkAsReadAsync(Guid uid, DateTimeOffset readAt, CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var ownsConnection = _boundConnection == null;

        try
        {
            var command = new CommandDefinition(
                """
                UPDATE notifications
                SET read_at = @ReadAt
                WHERE uid = @Uid AND read_at IS NULL;
                """,
                new
                {
                    Uid = uid.ToString(),
                    ReadAt = readAt.ToUnixTimeMilliseconds()
                },
                transaction: GetTransaction(),
                cancellationToken: cancellationToken);

            await connection.ExecuteAsync(command);
        }
        finally
        {
            if (ownsConnection)
                connection.Dispose();
        }
    }

    public async Task DismissAsync(Guid uid, DateTimeOffset dismissedAt, CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var ownsConnection = _boundConnection == null;

        try
        {
            var command = new CommandDefinition(
                """
                UPDATE notifications
                SET dismissed_at = @DismissedAt, read_at = COALESCE(read_at, @DismissedAt)
                WHERE uid = @Uid AND dismissed_at IS NULL;
                """,
                new
                {
                    Uid = uid.ToString(),
                    DismissedAt = dismissedAt.ToUnixTimeMilliseconds()
                },
                transaction: GetTransaction(),
                cancellationToken: cancellationToken);

            await connection.ExecuteAsync(command);
        }
        finally
        {
            if (ownsConnection)
                connection.Dispose();
        }
    }

    private static Notification MapRow(dynamic row)
    {
        return new Notification(
            Id: (int)row.id,
            Uid: Guid.Parse((string)row.uid),
            UserId: (int?)row.user_id,
            Type: (string)row.type,
            Severity: (string)row.severity,
            Title: (string)row.title,
            Body: (string)row.body,
            Source: (string)row.source,
            ActionJson: (string?)row.action_json,
            CreatedAt: DateTimeOffset.FromUnixTimeMilliseconds((long)row.created_at),
            ReadAt: row.read_at != null ? DateTimeOffset.FromUnixTimeMilliseconds((long)row.read_at) : null,
            DismissedAt: row.dismissed_at != null ? DateTimeOffset.FromUnixTimeMilliseconds((long)row.dismissed_at) : null
        );
    }
}
