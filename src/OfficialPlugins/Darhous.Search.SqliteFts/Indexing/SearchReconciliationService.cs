using Dapper;
using Darhous.Archive.Application.Persistence;
using Darhous.Archive.Contracts.Documents;
using Darhous.Archive.Persistence.Configuration;
using Darhous.Archive.Persistence.Connections;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Darhous.Search.SqliteFts.Indexing;

/// <summary>
/// Sole mechanism keeping search.db in sync with archive.db (Phase 8 design decision —
/// deliberately simpler than the Transient-event-bus fast path originally reviewed with
/// Codex/AgentFlow Level 2: every document mutation already bumps <c>documents.updated_at</c>
/// (Phase 2's <c>ix_documents_updated_at</c>, built ahead of need for exactly this), so a
/// periodic diff against that column catches every add/move/trash/restore/bulk-move/
/// folder-cascade-move automatically — without instrumenting every call site in
/// DocumentService/FolderService/BulkOperationService with event publishing, and without the
/// cross-db Outbox coupling risk the design review flagged. The only gap an updated_at diff
/// can't see is a permanent delete (the row disappears entirely, updated_at tells you
/// nothing) — the periodic full sweep exists specifically to catch that. SAD §128 (Eventual
/// Consistency) explicitly allows this bounded staleness for search.db.
/// </summary>
public sealed class SearchReconciliationService(
    IDocumentRepository documentRepository,
    IDocumentVersionRepository documentVersionRepository,
    ISearchIndexWriter indexWriter,
    ISqliteConnectionFactory connectionFactory,
    SearchIndexingOptions options,
    ILogger<SearchReconciliationService> logger)
    : BackgroundService
{
    private DateTimeOffset _checkpoint = DateTimeOffset.MinValue;
    private int _tickCount;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _checkpoint = await ReadCheckpointAsync(stoppingToken);

        using var timer = new PeriodicTimer(options.ReconciliationInterval);
        do
        {
            try
            {
                await RunIncrementalSweepAsync(stoppingToken);

                _tickCount++;
                if (_tickCount % Math.Max(1, options.FullSweepEveryNTicks) == 0)
                {
                    await RunFullSweepAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A bad sweep must not kill the background loop (SAD §5.1) — the next tick
                // simply tries again from the same checkpoint.
                logger.LogError(ex, "Search reconciliation sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Re-indexes every document whose updated_at moved past the last checkpoint. Public for direct invocation from tests without waiting on the timer.</summary>
    public async Task RunIncrementalSweepAsync(CancellationToken cancellationToken)
    {
        var changed = await documentRepository.ListUpdatedSinceAsync(_checkpoint, cancellationToken);
        if (changed.Count == 0)
        {
            return;
        }

        foreach (var document in changed)
        {
            await ReindexOrRemoveAsync(document, cancellationToken);
        }

        _checkpoint = changed[^1].UpdatedAt;
    }

    /// <summary>
    /// Diffs every active document uid against search_state and removes orphans — the only
    /// way to catch a permanent delete, since the row is simply gone with no updated_at to
    /// observe. Also re-indexes anything present in archive.db but missing from search_state
    /// entirely (first run, or a checkpoint reset). Public for direct test invocation.
    /// </summary>
    public async Task RunFullSweepAsync(CancellationToken cancellationToken)
    {
        var allDocuments = await documentRepository.ListAsync(cancellationToken);
        var activeByUid = allDocuments.Where(d => d.DeletedAt is null).ToDictionary(d => d.Uid);

        var indexedUids = await ReadIndexedUidsAsync(cancellationToken);

        foreach (var uid in indexedUids)
        {
            if (!activeByUid.ContainsKey(uid))
            {
                await indexWriter.RemoveAsync(uid, cancellationToken);
            }
        }

        foreach (var document in activeByUid.Values)
        {
            if (!indexedUids.Contains(document.Uid))
            {
                await ReindexOrRemoveAsync(document, cancellationToken);
            }
        }
    }

    private async Task ReindexOrRemoveAsync(Document document, CancellationToken cancellationToken)
    {
        if (document.DeletedAt is not null || document.Status == DocumentStatus.Trashed)
        {
            // DB Spec §182: trashed documents leave the active FTS index.
            await indexWriter.RemoveAsync(document.Uid, cancellationToken);
            return;
        }

        var (fileName, fileType) = await ResolveFileInfoAsync(document, cancellationToken);

        await indexWriter.UpsertAsync(
            new SearchDocumentSnapshot(
                document.Uid, document.ArchiveNumber, document.Title, fileName, fileType,
                document.FolderId, document.ArchiveDate, document.Status.ToString(), document.UpdatedAt),
            cancellationToken);
    }

    private async Task<(string FileName, string? FileType)> ResolveFileInfoAsync(Document document, CancellationToken cancellationToken)
    {
        if (document.CurrentVersionId is not { } versionUid)
        {
            return (document.Title, null);
        }

        var version = await documentVersionRepository.GetByUidAsync(versionUid, cancellationToken);
        if (version is null)
        {
            return (document.Title, null);
        }

        var fileType = version.FileExtension.TrimStart('.').ToLowerInvariant();
        return (version.OriginalFileName, fileType.Length == 0 ? null : fileType);
    }

    private async Task<DateTimeOffset> ReadCheckpointAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(DatabaseKind.Search, cancellationToken);
        var maxMillis = await connection.QuerySingleOrDefaultAsync<long?>(
            new CommandDefinition("SELECT MAX(source_updated_at) FROM search_state;", cancellationToken: cancellationToken));

        return maxMillis is { } millis ? DateTimeOffset.FromUnixTimeMilliseconds(millis) : DateTimeOffset.MinValue;
    }

    private async Task<HashSet<Guid>> ReadIndexedUidsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(DatabaseKind.Search, cancellationToken);
        var uids = await connection.QueryAsync<string>(
            new CommandDefinition("SELECT document_uid FROM search_state;", cancellationToken: cancellationToken));

        return uids.Select(Guid.Parse).ToHashSet();
    }
}
