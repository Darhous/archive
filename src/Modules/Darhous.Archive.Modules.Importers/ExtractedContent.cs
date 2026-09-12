namespace Darhous.Archive.Modules.Importers;

/// <summary>
/// Implementation Plan §55-58. <paramref name="Text"/> is the searchable body (document text or
/// email body — Search's <c>documents_fts.body</c> column is the natural home for it once a
/// caller wires this into indexing; this project only produces it, it doesn't publish it
/// anywhere itself). <paramref name="MetadataJson"/> holds whatever extra structured fields a
/// format has (PDF Title/Author, email Subject/From/To/Cc/Date) — deliberately loose rather
/// than a rigid shared schema, since the fields genuinely differ per format.
/// </summary>
public sealed record ExtractedContent(string? Text, int? PageCount, bool? IsSearchablePdf, string? MetadataJson);
