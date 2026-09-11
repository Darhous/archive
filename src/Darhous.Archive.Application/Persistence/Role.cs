namespace Darhous.Archive.Application.Persistence;

/// <summary>DB Spec (roles table) — public shape; the SQLite rowid never leaves Persistence.</summary>
public sealed record Role(Guid Uid, string Code, string DisplayName, bool IsSystem, DateTimeOffset CreatedAt);
