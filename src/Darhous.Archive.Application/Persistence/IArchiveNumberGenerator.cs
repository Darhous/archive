namespace Darhous.Archive.Application.Persistence;

/// <summary>
/// SAD §27 (archive_number) — pattern "ARC-{YYYY}-{000001}", monotonic per year, never reused
/// once assigned (DB Spec §28 number_sequences). Must run inside the same transaction as the
/// document insert it's for — the write queue already serializes every write, so a plain
/// read-increment-write is safe without extra locking.
/// </summary>
public interface IArchiveNumberGenerator
{
    Task<string> GenerateAsync(int year, CancellationToken cancellationToken);
}
