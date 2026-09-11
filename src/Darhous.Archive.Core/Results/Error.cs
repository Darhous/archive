namespace Darhous.Archive.Core.Results;

/// <summary>
/// Implementation Plan §30 (Result Model) — ErrorCode / Message / TechnicalDetails / IsTransient.
/// </summary>
public sealed record Error(string Code, string Message, string? TechnicalDetails = null, bool IsTransient = false)
{
    public static Error Of(string code, string message, string? technicalDetails = null, bool isTransient = false) =>
        new(code, message, technicalDetails, isTransient);
}
