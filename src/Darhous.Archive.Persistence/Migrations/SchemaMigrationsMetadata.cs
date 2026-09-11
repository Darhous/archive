using FluentMigrator.Runner.VersionTableInfo;

namespace Darhous.Archive.Persistence.Migrations;

/// <summary>
/// Names FluentMigrator's own bookkeeping table `schema_migrations` (DB Spec §16) instead
/// of its default `VersionInfo`. Registered explicitly in DI per database (see
/// <c>MigrationRunnerFactory</c>) — FluentMigrator, not the initial migration, owns this
/// table, so the initial migration must never try to create it itself.
/// </summary>
[VersionTableMetaData]
public sealed class SchemaMigrationsMetadata : IVersionTableMetaData
{
    public string SchemaName => string.Empty;
    public string TableName => "schema_migrations";
    public string ColumnName => "version";
    public string DescriptionColumnName => "description";
    public string AppliedOnColumnName => "applied_at";
    public string UniqueIndexName => "ux_schema_migrations_version";
    public bool OwnsSchema => true;
    public bool CreateWithPrimaryKey => true;
    public object? ApplicationContext { get; set; }
}
