using System.Text;

namespace Darhous.Archive.Core.Text;

/// <summary>
/// SAD §24.3 (دعم العربية) — the exact rule set the master doc specifies: unify alef
/// variants, unify ya/alef-maksura, strip diacritics and tatweel, collapse whitespace.
/// Needed now (Phase 5, `documents.title_normalized`) even though the full search
/// pipeline (FTS5 indexing, ranking, snippets) is Phase 8 — both consume this same
/// function, so it lives here in Core rather than being duplicated later.
/// Never mutates the original text; used only for the searchable/comparable copy.
/// </summary>
public static class ArabicNormalization
{
    // Arabic diacritics (tashkeel) + tatweel (U+0640), per SAD §24.3.
    private static readonly char[] DiacriticsAndTatweel =
    [
        'ً', 'ٌ', 'ٍ', 'َ', 'ُ', 'ِ', 'ّ', 'ْ',
        'ٓ', 'ٔ', 'ٕ', 'ٖ', 'ٗ', '٘', 'ٰ', 'ـ',
    ];

    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (Array.IndexOf(DiacriticsAndTatweel, ch) >= 0)
            {
                continue;
            }

            builder.Append(ch switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ى' => 'ي',
                _ => ch,
            });
        }

        // Collapse runs of whitespace to a single space and trim.
        var normalized = builder.ToString();
        var result = new StringBuilder(normalized.Length);
        var lastWasSpace = false;
        foreach (var ch in normalized.Trim())
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace)
                {
                    result.Append(' ');
                }

                lastWasSpace = true;
            }
            else
            {
                result.Append(ch);
                lastWasSpace = false;
            }
        }

        return result.ToString();
    }
}
