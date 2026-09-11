namespace Darhous.Archive.Persistence.Configuration;

/// <summary>
/// DB Spec §7 (Database splitting) — three independent SQLite files, deliberately not
/// cross-referenced by real SQLite foreign keys (SQLite cannot enforce FKs across files).
/// archive.db is the transactional store; audit.db and search.db are eventually consistent
/// with it via the Outbox (SAD §5.4 — "Search Index Is Not Source of Truth").
/// </summary>
public enum DatabaseKind
{
    Archive,
    Audit,
    Search,
}
