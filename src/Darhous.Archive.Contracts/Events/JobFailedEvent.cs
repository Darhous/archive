using System;

namespace Darhous.Archive.Contracts.Events;

public record JobFailedEvent(
    string JobType,
    Guid JobUid,
    string ErrorMessage,
    string Severity = "error"
);
