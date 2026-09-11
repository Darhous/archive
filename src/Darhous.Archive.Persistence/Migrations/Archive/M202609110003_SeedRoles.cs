using FluentMigrator;

namespace Darhous.Archive.Persistence.Migrations.Archive;

/// <summary>
/// DB Spec §18 (roles) — "الأدوار الأساسية Seeded: admin/user/readonly/guest". System roles,
/// never deleted. Uses FluentMigrator's parameterized Insert API rather than Execute.Sql
/// string interpolation (DB Spec §182 SQL Injection Rule — no raw-SQL value building, even
/// for constants known at migration-authoring time; consistency matters more than the
/// injection risk being nil here).
/// </summary>
[Tags("Archive")]
[Migration(202609110003)]
public sealed class M202609110003_SeedRoles : Migration
{
    public override void Up()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        foreach (var (code, displayName) in new[]
                 {
                     ("admin", "Admin"), ("user", "User"), ("readonly", "Read Only"), ("guest", "Guest"),
                 })
        {
            Insert.IntoTable("roles").Row(new
            {
                uid = Guid.CreateVersion7().ToString(),
                code,
                display_name = displayName,
                is_system = 1,
                created_at = now,
            });
        }
    }

    public override void Down()
    {
        Delete.FromTable("roles").Row(new { code = "admin" });
        Delete.FromTable("roles").Row(new { code = "user" });
        Delete.FromTable("roles").Row(new { code = "readonly" });
        Delete.FromTable("roles").Row(new { code = "guest" });
    }
}
