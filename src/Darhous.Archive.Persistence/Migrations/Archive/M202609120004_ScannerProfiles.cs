using FluentMigrator;

namespace Darhous.Archive.Persistence.Migrations.Archive;

[Tags("Archive")]
[Migration(202609120004)]
public sealed class M202609120004_ScannerProfiles : Migration
{
    public override void Up()
    {
        Execute.Sql(
            """
            CREATE TABLE scanner_profiles (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                resolution INTEGER NOT NULL,
                color_mode TEXT NOT NULL,
                duplex INTEGER NOT NULL,
                source TEXT NOT NULL,
                page_separation_mode TEXT NOT NULL,
                is_seed INTEGER NOT NULL DEFAULT 0
            );
            """);

        InsertProfile("A4 Single 300 DPI OCR Arabic", 300, "bw", 0, "flatbed", "single");
        InsertProfile("A4 Every Page", 300, "color", 0, "adf", "every_page");
        InsertProfile("A4 Every 2 Pages", 300, "color", 0, "adf", "every_2_pages");
        InsertProfile("Duplex", 300, "color", 1, "adf", "duplex");
        InsertProfile("Flatbed", 300, "color", 0, "flatbed", "flatbed");
    }

    public override void Down()
    {
        Execute.Sql("DROP TABLE IF EXISTS scanner_profiles;");
    }

    private void InsertProfile(string name, int resolution, string colorMode, int duplex, string source, string sepMode)
    {
        Execute.Sql(
            $"""
            INSERT INTO scanner_profiles (name, resolution, color_mode, duplex, source, page_separation_mode, is_seed)
            SELECT '{name}', {resolution}, '{colorMode}', {duplex}, '{source}', '{sepMode}', 1
            WHERE NOT EXISTS (SELECT 1 FROM scanner_profiles WHERE name = '{name}');
            """);
    }
}
