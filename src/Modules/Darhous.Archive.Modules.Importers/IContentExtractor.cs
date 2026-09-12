namespace Darhous.Archive.Modules.Importers;

public interface IContentExtractor
{
    bool CanHandle(string fileExtension);

    Task<ExtractedContent> ExtractAsync(string filePath, CancellationToken cancellationToken);
}
