using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Microsoft.Data.Sqlite;

namespace Darhous.Archive.Persistence.Tests;

/// <summary>
/// Proves DB Spec §107.1 (ON DELETE Policy) actually behaves as documented — and that the
/// NULL-uniqueness fix for folder sibling names (DB Spec §34) really blocks duplicates.
/// </summary>
public class ForeignKeyPolicyTests : PersistenceTestBase
{
    [Fact]
    public async Task DeletingFolder_SetsDocumentFolderIdToNull()
    {
        await using var connection = await OpenAsync();

        var roleId = await InsertRoleAsync(connection);
        var userId = await InsertUserAsync(connection, roleId);
        var folderId = await InsertFolderAsync(connection, userId, "Traffic");
        var documentId = await InsertDocumentAsync(connection, userId, folderId, "ARC-2026-000001");

        await ExecuteAsync(connection, "DELETE FROM folders WHERE id = @Id;", ("@Id", folderId));

        var actualFolderId = await ScalarAsync(connection, "SELECT folder_id FROM documents WHERE id = @Id;", ("@Id", documentId));
        Assert.Equal(DBNull.Value, actualFolderId);
    }

    [Fact]
    public async Task DeletingDocument_CascadesToVersionsAndTags()
    {
        await using var connection = await OpenAsync();

        var roleId = await InsertRoleAsync(connection);
        var userId = await InsertUserAsync(connection, roleId);
        var documentId = await InsertDocumentAsync(connection, userId, folderId: null, "ARC-2026-000002");
        await InsertVersionAsync(connection, documentId, userId, versionNo: 1);
        var tagId = await InsertTagAsync(connection, userId, "urgent");
        await ExecuteAsync(connection,
            "INSERT INTO document_tags (document_id, tag_id, added_by, added_at) VALUES (@DocId, @TagId, @UserId, @Now);",
            ("@DocId", documentId), ("@TagId", tagId), ("@UserId", userId), ("@Now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

        await ExecuteAsync(connection, "DELETE FROM documents WHERE id = @Id;", ("@Id", documentId));

        var versionCount = (long)(await ScalarAsync(connection, "SELECT COUNT(*) FROM document_versions WHERE document_id = @Id;", ("@Id", documentId)))!;
        var tagLinkCount = (long)(await ScalarAsync(connection, "SELECT COUNT(*) FROM document_tags WHERE document_id = @Id;", ("@Id", documentId)))!;

        Assert.Equal(0, versionCount);
        Assert.Equal(0, tagLinkCount);
    }

    [Fact]
    public async Task DeletingRoleInUse_IsBlockedByRestrict()
    {
        await using var connection = await OpenAsync();

        var roleId = await InsertRoleAsync(connection, code: "user_role_in_use");
        await InsertUserAsync(connection, roleId);

        var ex = await Assert.ThrowsAsync<SqliteException>(() =>
            ExecuteAsync(connection, "DELETE FROM roles WHERE id = @Id;", ("@Id", roleId)));

        Assert.Contains("FOREIGN KEY constraint failed", ex.Message);
    }

    [Fact]
    public async Task DeletingCurrentVersion_IsBlockedByRestrict_UntilReassigned()
    {
        await using var connection = await OpenAsync();

        var roleId = await InsertRoleAsync(connection);
        var userId = await InsertUserAsync(connection, roleId);
        var documentId = await InsertDocumentAsync(connection, userId, folderId: null, "ARC-2026-000003");
        var versionId = await InsertVersionAsync(connection, documentId, userId, versionNo: 1);
        await ExecuteAsync(connection, "UPDATE documents SET current_version_id = @VersionId WHERE id = @DocId;",
            ("@VersionId", versionId), ("@DocId", documentId));

        // Deleting the version that is still "current" must be blocked, not silently allowed
        // to dangle (RESTRICT, not SET NULL — DB Spec §107.1 rationale: explicit reassignment).
        await Assert.ThrowsAsync<SqliteException>(() =>
            ExecuteAsync(connection, "DELETE FROM document_versions WHERE id = @Id;", ("@Id", versionId)));

        // Reassign, then the delete succeeds.
        await ExecuteAsync(connection, "UPDATE documents SET current_version_id = NULL WHERE id = @Id;", ("@Id", documentId));
        await ExecuteAsync(connection, "DELETE FROM document_versions WHERE id = @Id;", ("@Id", versionId));
    }

    [Fact]
    public async Task DuplicateRootLevelFolderNames_AreBlockedDespiteNullParentId()
    {
        await using var connection = await OpenAsync();
        var roleId = await InsertRoleAsync(connection);
        var userId = await InsertUserAsync(connection, roleId);

        await InsertFolderAsync(connection, userId, "المرور");

        // Same name, still no parent (root level) — must be rejected by
        // ux_folders_sibling_name (the COALESCE(parent_id, 0) expression index),
        // proving the plain UNIQUE(parent_id, name_normalized) NULL gotcha is actually fixed.
        await Assert.ThrowsAsync<SqliteException>(() => InsertFolderAsync(connection, userId, "المرور"));
    }

    [Fact]
    public async Task ForeignKeyCheck_IsCleanAfterTypicalWrites()
    {
        await using var connection = await OpenAsync();

        var roleId = await InsertRoleAsync(connection);
        var userId = await InsertUserAsync(connection, roleId);
        var folderId = await InsertFolderAsync(connection, userId, "الحماية");
        var documentId = await InsertDocumentAsync(connection, userId, folderId, "ARC-2026-000004");
        await InsertVersionAsync(connection, documentId, userId, versionNo: 1);

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.False(await reader.ReadAsync(), "PRAGMA foreign_key_check reported a violation.");
    }

    private async Task<SqliteConnection> OpenAsync() =>
        await new SqliteConnectionFactory(Options).OpenAsync(DatabaseKind.Archive, CancellationToken.None);

    // "admin"/"user"/"readonly"/"guest" are seeded by M202609110003_SeedRoles (Phase 3) —
    // default to a fresh, never-colliding code so tests here don't depend on the seed data.
    private static async Task<long> InsertRoleAsync(SqliteConnection connection, string? code = null)
    {
        code ??= $"test-role-{Guid.NewGuid():N}";

        await ExecuteAsync(connection,
            "INSERT INTO roles (uid, code, display_name, is_system, created_at) VALUES (@Uid, @Code, @DisplayName, 0, @Now);",
            ("@Uid", Guid.NewGuid().ToString()), ("@Code", code), ("@DisplayName", code), ("@Now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        return (long)(await ScalarAsync(connection, "SELECT last_insert_rowid();"))!;
    }

    private static async Task<long> InsertUserAsync(SqliteConnection connection, long roleId)
    {
        await ExecuteAsync(connection,
            """
            INSERT INTO app_users (uid, username, display_name, password_hash, password_scheme, role_id, created_at, updated_at)
            VALUES (@Uid, @Username, @DisplayName, 'hash', 'argon2id', @RoleId, @Now, @Now);
            """,
            ("@Uid", Guid.NewGuid().ToString()), ("@Username", $"user_{Guid.NewGuid():N}"),
            ("@DisplayName", "Test User"), ("@RoleId", roleId), ("@Now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        return (long)(await ScalarAsync(connection, "SELECT last_insert_rowid();"))!;
    }

    private static async Task<long> InsertFolderAsync(SqliteConnection connection, long userId, string name)
    {
        await ExecuteAsync(connection,
            """
            INSERT INTO folders (uid, parent_id, name, name_normalized, sort_order, is_system, is_hidden, created_by, created_at, updated_at)
            VALUES (@Uid, NULL, @Name, @Name, 0, 0, 0, @UserId, @Now, @Now);
            """,
            ("@Uid", Guid.NewGuid().ToString()), ("@Name", name), ("@UserId", userId), ("@Now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        return (long)(await ScalarAsync(connection, "SELECT last_insert_rowid();"))!;
    }

    private static async Task<long> InsertDocumentAsync(SqliteConnection connection, long userId, long? folderId, string archiveNumber)
    {
        await ExecuteAsync(connection,
            """
            INSERT INTO documents
                (uid, archive_number, title, title_normalized, folder_id, status, source_type, storage_mode, archive_date, created_by, created_at, updated_by, updated_at)
            VALUES
                (@Uid, @ArchiveNumber, @Title, @Title, @FolderId, 'active', 'manual', 'managed', @ArchiveDate, @UserId, @Now, @UserId, @Now);
            """,
            ("@Uid", Guid.NewGuid().ToString()), ("@ArchiveNumber", archiveNumber), ("@Title", "Test Document"),
            ("@FolderId", (object?)folderId ?? DBNull.Value), ("@ArchiveDate", DateTimeOffset.UtcNow.ToString("yyyy-MM-dd")),
            ("@UserId", userId), ("@Now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        return (long)(await ScalarAsync(connection, "SELECT last_insert_rowid();"))!;
    }

    private static async Task<long> InsertVersionAsync(SqliteConnection connection, long documentId, long userId, int versionNo)
    {
        await ExecuteAsync(connection,
            """
            INSERT INTO document_versions
                (uid, document_id, version_no, original_file_name, stored_file_name, file_path, file_extension,
                 file_size, sha256, imported_at, created_by, availability_status, content_extraction_status)
            VALUES
                (@Uid, @DocumentId, @VersionNo, 'doc.pdf', 'stored.pdf', 'C:\\storage\\stored.pdf', '.pdf',
                 1024, @Sha256, @Now, @UserId, 'available', 'pending');
            """,
            ("@Uid", Guid.NewGuid().ToString()), ("@DocumentId", documentId), ("@VersionNo", versionNo),
            ("@Sha256", Guid.NewGuid().ToString("N")), ("@Now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), ("@UserId", userId));
        return (long)(await ScalarAsync(connection, "SELECT last_insert_rowid();"))!;
    }

    private static async Task<long> InsertTagAsync(SqliteConnection connection, long userId, string name)
    {
        await ExecuteAsync(connection,
            "INSERT INTO tags (uid, name, name_normalized, created_by, created_at) VALUES (@Uid, @Name, @Name, @UserId, @Now);",
            ("@Uid", Guid.NewGuid().ToString()), ("@Name", name), ("@UserId", userId), ("@Now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        return (long)(await ScalarAsync(connection, "SELECT last_insert_rowid();"))!;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql, params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return await command.ExecuteScalarAsync();
    }
}
