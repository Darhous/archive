using System.Data;
using Dapper;
using Darhous.Archive.Application.Persistence;

namespace Darhous.Archive.Persistence.Repositories;

/// <summary>
/// SAD §27 / DB Spec §28 (number_sequences) — one row per sequence key, reset to 1 whenever
/// the year changes. Only ever called from inside a write-queue transaction (there is exactly
/// one writer per database), so a plain read-then-write is race-free by construction — no
/// extra locking needed.
/// </summary>
public sealed class ArchiveNumberGenerator(IDbConnection connection, IDbTransaction transaction) : IArchiveNumberGenerator
{
    private const string SequenceKey = "archive_number";

    public async Task<string> GenerateAsync(int year, CancellationToken cancellationToken)
    {
        var existing = await connection.QuerySingleOrDefaultAsync<SequenceRow>(new CommandDefinition(
            "SELECT year, last_value FROM number_sequences WHERE sequence_key = @Key;",
            new { Key = SequenceKey }, transaction, cancellationToken: cancellationToken));

        var nextValue = existing is null || existing.Year != year ? 1 : existing.LastValue + 1;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (existing is null)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO number_sequences (sequence_key, year, last_value, updated_at) VALUES (@Key, @Year, @Value, @Now);",
                new { Key = SequenceKey, Year = year, Value = nextValue, Now = now }, transaction, cancellationToken: cancellationToken));
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE number_sequences SET year = @Year, last_value = @Value, updated_at = @Now WHERE sequence_key = @Key;",
                new { Key = SequenceKey, Year = year, Value = nextValue, Now = now }, transaction, cancellationToken: cancellationToken));
        }

        return $"ARC-{year}-{nextValue:D6}";
    }

    private sealed class SequenceRow
    {
        public int Year { get; set; }
        public int LastValue { get; set; }
    }
}
