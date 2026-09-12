using System;

namespace Darhous.Archive.Modules.Notifications;

public record Notification(
    int Id,
    Guid Uid,
    int? UserId,
    string Type,
    string Severity,
    string Title,
    string Body,
    string Source,
    string? ActionJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt,
    DateTimeOffset? DismissedAt
);
