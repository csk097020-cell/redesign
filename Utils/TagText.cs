using System.Text;
using System.Text.RegularExpressions;
using MomentaryMomentos.Models;

namespace MomentaryMomentos.Utils;

/// <summary>
/// Tag name normalization + case-insensitive dedupe helpers.
///
/// The app stores/displays tags in Title Case and treats them case-insensitively so
/// "sad", "Sad", and "SAD" collapse to a single "Sad". <see cref="Normalize"/> mirrors
/// Postgres <c>initcap(btrim(name))</c> used by the 004 merge migration so app-created
/// names line up with the DB unique index (momo_tags_unique_name_per_owner).
/// </summary>
public static class TagText
{
    /// <summary>
    /// Trim, collapse internal whitespace, and Title-Case each word (first alphanumeric of a
    /// word upper, rest lower) — matching Postgres <c>initcap</c> semantics.
    /// </summary>
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";

        var collapsed = Regex.Replace(raw.Trim(), @"\s+", " ");
        var sb = new StringBuilder(collapsed.Length);
        var prevAlnum = false;
        foreach (var ch in collapsed)
        {
            var isAlnum = char.IsLetterOrDigit(ch);
            if (isAlnum)
                sb.Append(prevAlnum ? char.ToLowerInvariant(ch) : char.ToUpperInvariant(ch));
            else
                sb.Append(ch);
            prevAlnum = isAlnum;
        }
        return sb.ToString();
    }

    /// <summary>Case-insensitive comparison key for a tag name (normalized, lower-cased).</summary>
    public static string Key(string? raw) => Normalize(raw).ToLowerInvariant();

    /// <summary>True when two names refer to the same tag ignoring case/whitespace.</summary>
    public static bool SameName(string? a, string? b) => Key(a) == Key(b);

    /// <summary>
    /// Collapse tags that share a case-insensitive name to a single survivor. By default the
    /// active tag wins; pass <paramref name="prefer"/> (e.g. "is applied to this memory") to
    /// keep a specific one so an existing selection still resolves.
    /// </summary>
    public static List<Tag> Dedupe(IEnumerable<Tag> tags, Func<Tag, bool>? prefer = null)
    {
        return tags
            .GroupBy(t => Key(t.Name))
            .Select(g =>
            {
                IOrderedEnumerable<Tag> ordered = prefer is not null
                    ? g.OrderByDescending(prefer).ThenByDescending(t => t.IsActive)
                    : g.OrderByDescending(t => t.IsActive);
                return ordered.First();
            })
            .ToList();
    }
}
