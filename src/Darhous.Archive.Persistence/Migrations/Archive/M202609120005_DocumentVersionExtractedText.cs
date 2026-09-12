using FluentMigrator;

namespace Darhous.Archive.Persistence.Migrations.Archive;

/// <summary>Phase 15 (OCR) — durable extracted text and the optional derived searchable PDF path.</summary>
[Tags("Archive")]
[Migration(202609120005)]
public sealed class M202609120005_DocumentVersionExtractedText : Migration
{
    public override void Up()
    {
        Alter.Table("document_versions")
            .AddColumn("extracted_text").AsString(int.MaxValue).Nullable()
            .AddColumn("searchable_file_path").AsString(int.MaxValue).Nullable();
    }

    public override void Down()
    {
        Delete.Column("searchable_file_path").FromTable("document_versions");
        Delete.Column("extracted_text").FromTable("document_versions");
    }
}
