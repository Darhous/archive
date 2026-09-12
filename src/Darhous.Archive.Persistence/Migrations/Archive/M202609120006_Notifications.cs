using FluentMigrator;

namespace Darhous.Archive.Persistence.Migrations.Archive;

[Tags("Archive")]
[Migration(202609120006)]
public sealed class M202609120006_Notifications : Migration
{
    public override void Up()
    {
        Execute.Sql(
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

    public override void Down()
    {
        Execute.Sql("DROP TABLE IF EXISTS notifications;");
    }
}
