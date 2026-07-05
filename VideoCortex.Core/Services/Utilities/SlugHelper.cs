using System.Globalization;
using System.Text;

namespace VideoCortex.Core.Services.Utilities;

/// <summary>
/// Produces URL/DB-safe slugs (lowercase, hyphenated, <c>^[A-Za-z0-9._-]+$</c>). Slugs are
/// used only for the <c>/projects/{slug}</c> route and the DB unique index — never for the
/// on-disk library folder name (that keeps the human display name; see OkfLibraryStore).
/// </summary>
public static class SlugHelper
{
    /// <summary>
    /// Lowercase, collapse non-alphanumeric runs to a single hyphen, trim hyphens.
    /// Returns the supplied <paramref name="fallback"/> when the input collapses to empty.
    /// </summary>
    public static string ToSlug(string? input, string fallback)
    {
        if (string.IsNullOrWhiteSpace(input)) return fallback;

        // Decompose accented characters (é → e + combining acute) and drop the combining
        // marks so diacritics transliterate to their base ASCII letter (Café → cafe). Then
        // keep only ASCII alphanumerics — everything else collapses to a single hyphen. This
        // guarantees the result matches ^[A-Za-z0-9._-]+$ (the URL/DB-safe invariant).
        var decomposed = input.Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder(decomposed.Length);
        var lastWasHyphen = true; // suppress leading hyphens
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue; // drop combining diacritic marks

            var isAsciiAlnum = ch is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9');
            if (isAsciiAlnum)
            {
                sb.Append(char.ToLowerInvariant(ch));
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen)
            {
                sb.Append('-');
                lastWasHyphen = true;
            }
        }
        if (sb.Length > 0 && sb[^1] == '-') sb.Length--;
        return sb.Length == 0 ? fallback : sb.ToString();
    }

    /// <summary>
    /// Append <c>-2</c>, <c>-3</c>, … until <paramref name="exists"/> returns false.
    /// After 100 attempts, falls back to <c>{fallback}-{guid}</c> to break the loop.
    /// </summary>
    public static async Task<string> UniqueSlugAsync(
        string preferred, Func<string, Task<bool>> exists, string fallback)
    {
        var candidate = preferred;
        var n = 1;
        while (await exists(candidate))
        {
            n++;
            candidate = $"{preferred}-{n}";
            if (n > 100)
            {
                candidate = $"{fallback}-{Guid.NewGuid():N}"
                    .Substring(0, Math.Min(40, fallback.Length + 33));
                break;
            }
        }
        return candidate;
    }
}
