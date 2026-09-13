namespace Darhous.Archive.Modules.Updates;

internal sealed record UpdateJobPayload(
    string Operation,
    string? PackagePath,
    long? HistoryId,
    Guid? InitiatedBy);
