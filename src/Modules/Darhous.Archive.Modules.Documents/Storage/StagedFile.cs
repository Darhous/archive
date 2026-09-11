namespace Darhous.Archive.Modules.Documents.Storage;

/// <summary>A file copied to a temporary staging location, not yet at its permanent path.</summary>
public sealed record StagedFile(string TempPath, string FinalPath, Guid DocumentUid, int VersionNo, string FileExtension);
