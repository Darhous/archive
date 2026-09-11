using System.Data;
using System.Text.Json;
using Dapper;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Events;

namespace Darhous.Archive.Persistence.Outbox;

/// <summary>
/// Always runs bound to the ambient <see cref="IUnitOfWork"/> transaction (DB Spec §121
/// Outbox Consistency) — there is no read-only mode for this one, unlike
/// <c>RoleRepository</c>: an outbox row only ever makes sense written alongside a business
/// change in the same commit.
/// </summary>
public sealed class OutboxWriter(IDbConnection connection, IDbTransaction transaction) : IOutboxWriter
{
    public async Task EnqueueAsync<T>(
        string eventType,
        EventDeliveryLevel level,
        T payload,
        Guid? correlationId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        if (level == EventDeliveryLevel.Transient)
        {
            throw new ArgumentOutOfRangeException(nameof(level),
                "Transient events never touch the outbox table — publish them through IEventBus directly.");
        }

        // TODO(Phase 3 — Authentication & Roles, owner: whoever builds AppUserRepository):
        // outbox_events.user_id is an INTEGER FK to app_users.id (the internal rowid), but
        // this writer only ever receives the caller's public Guid `userId` (same convention
        // as ArchiveEventEnvelope/ArchivePrincipal). Resolving Guid uid -> internal id needs
        // an app_users lookup, which doesn't exist until Phase 3 owns that table. Until then
        // every outbox row's user_id is stored NULL regardless of the caller-supplied value.
        // Removal condition: wire the lookup here once IAppUserRepository exists.
        var command = new CommandDefinition(
            """
            INSERT INTO outbox_events
                (uid, event_type, delivery_level, payload_json, correlation_id, user_id, status, retry_count, created_at)
            VALUES
                (@Uid, @EventType, @DeliveryLevel, @PayloadJson, @CorrelationId, NULL, @Status, 0, @CreatedAt);
            """,
            new
            {
                Uid = Guid.CreateVersion7().ToString(),
                EventType = eventType,
                DeliveryLevel = level == EventDeliveryLevel.Critical ? "critical" : "reliable",
                PayloadJson = JsonSerializer.Serialize(payload),
                CorrelationId = correlationId?.ToString(),
                Status = "pending",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            },
            transaction,
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }
}
