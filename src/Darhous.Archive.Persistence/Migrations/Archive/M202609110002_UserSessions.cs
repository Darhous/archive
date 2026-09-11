using FluentMigrator;

namespace Darhous.Archive.Persistence.Migrations.Archive;

/// <summary>
/// DB Spec §21 (user_sessions) — Phase 3 (Authentication &amp; Roles) needs this for
/// Login/Logout/Switch User/Remember Me. `token_hash` stores a SHA-256 hash of the session
/// token, never the raw token (same principle as password hashing — DB Spec §21: "الـRemember
/// Me secret نفسه يحفظ محميًا... ولا يخزن Plain Text في DB").
/// </summary>
[Tags("Archive")]
[Migration(202609110002)]
public sealed class M202609110002_UserSessions : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            CREATE TABLE user_sessions (
                id INTEGER PRIMARY KEY,
                uid TEXT UNIQUE NOT NULL,
                user_id INTEGER NOT NULL
                    REFERENCES app_users (id) ON DELETE CASCADE,
                token_hash TEXT NOT NULL,
                created_at INTEGER NOT NULL,
                expires_at INTEGER NOT NULL,
                last_seen_at INTEGER NOT NULL,
                remember_me INTEGER NOT NULL DEFAULT 0 CHECK (remember_me IN (0, 1)),
                revoked_at INTEGER NULL
            );

            CREATE UNIQUE INDEX ux_user_sessions_token_hash ON user_sessions (token_hash);
            CREATE INDEX ix_user_sessions_user_id ON user_sessions (user_id);
            CREATE INDEX ix_user_sessions_expires_at ON user_sessions (expires_at);
            """);
    }

    public override void Down()
    {
        Execute.Sql("DROP TABLE IF EXISTS user_sessions;");
    }
}
