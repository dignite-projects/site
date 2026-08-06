using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Dignite.Site.Pages;

/// <summary>
/// The optional pattern on a <c>Page</c> that decides how the URLs of the contents beneath it are put
/// together (总体设计 §3.3): <c>{slug}</c> by default, or something like
/// <c>{publishTime:yyyy/MM}/{slug}</c> to get <c>/news/2026/07/my-post</c>.
/// <para>
/// Both directions live here, and they have to agree: <see cref="Build"/> composes a content's path when
/// a URL is emitted (sitemap, canonical, hreflang), and <see cref="TryExtractSlug"/> takes them apart
/// again when a request is routed. A pattern that builds one way and parses another produces URLs the
/// site itself 404s on.
/// </para>
/// <para>
/// This is routing, which is the page's job - it says nothing about whether the page renders as a single
/// page, a list or a detail view, so it does not reintroduce the "Kind" the model deliberately omits.
/// </para>
/// </summary>
public static class ContentPathPattern
{
    /// <summary>The pattern assumed when a page does not set one: the slug alone.</summary>
    public const string Default = "{slug}";

    private const string SlugToken = "{slug}";

    /// <summary>
    /// Matches <c>{publishTime:FORMAT}</c>, capturing FORMAT. Non-greedy and excluding <c>}</c> so two
    /// placeholders in one pattern cannot be swallowed as a single match.
    /// <para>
    /// Named after the content's own <c>PublishTime</c> field, not a generic word like "date" - the
    /// same rule that makes <c>{slug}</c> recognizable applies here: a placeholder is only ever the name
    /// of something the content actually has, never a keyword invented on top of it. A pattern that used
    /// an invented word would look plausible and still be wrong, the way <c>{year}</c>/<c>{month}</c>
    /// once slipped past validation entirely (see <see cref="IsValid"/>'s remarks).
    /// </para>
    /// </summary>
    private static readonly Regex PublishTimePlaceholder = new(@"\{publishTime:([^}]+)\}", RegexOptions.Compiled);

    /// <summary>
    /// The date format specifiers a pattern may use, longest first - <c>yyyy</c> has to be tried before
    /// <c>yy</c>, or the regex built for it would only ever match two of the four digits.
    /// </summary>
    private static readonly (string Specifier, string Pattern)[] DateSpecifiers =
    {
        ("yyyy", @"\d{4}"),
        ("yy", @"\d{2}"),
        ("MM", @"\d{2}"),
        ("M", @"\d{1,2}"),
        ("dd", @"\d{2}"),
        ("d", @"\d{1,2}")
    };

    /// <summary>
    /// Whether <paramref name="pattern"/> is usable. A pattern must contain <c>{slug}</c>: the slug is
    /// what makes a content's URL unique within its page, so a pattern without one would route every
    /// content beneath the page to the same path.
    /// <para>
    /// Every other <c>{...}</c> token in the pattern must be one this class actually understands - a
    /// <c>{publishTime:FORMAT}</c> placeholder, the only other kind <see cref="Build"/> and
    /// <see cref="TryExtractSlug"/> know how to handle. Anything else - a typo, a token from a different
    /// vocabulary someone remembered - is not an error either method raises; both simply treat unknown
    /// text as a literal, so a pattern like <c>{year}/{slug}</c> would build URLs that carry the literal
    /// string <c>{year}</c> forever and never resolve. Catching it here, at the one place a pattern is
    /// accepted, is what stops that from being a silent trap discovered only after content exists at the
    /// bad URL.
    /// </para>
    /// </summary>
    public static bool IsValid(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern) || !pattern.Contains(SlugToken, StringComparison.Ordinal))
        {
            return false;
        }

        var withoutRecognizedPlaceholders = PublishTimePlaceholder
            .Replace(pattern, string.Empty)
            .Replace(SlugToken, string.Empty, StringComparison.Ordinal);

        return !withoutRecognizedPlaceholders.Contains('{') && !withoutRecognizedPlaceholders.Contains('}');
    }

    /// <summary>
    /// Normalizes a stored pattern for use: null/blank falls back to <see cref="Default"/>, and any
    /// surrounding slashes are trimmed so callers can join it to a page route without doubling them.
    /// </summary>
    public static string Normalize(string? pattern)
    {
        return string.IsNullOrWhiteSpace(pattern)
            ? Default
            : pattern.Trim().Trim('/');
    }

    /// <summary>
    /// Renders the path of one content relative to its page route, e.g. <c>2026/07/my-post</c>.
    /// </summary>
    /// <param name="pattern">The owning page's pattern; null/blank means <see cref="Default"/>.</param>
    /// <param name="publishTime">Feeds any <c>{publishTime:...}</c> placeholder.</param>
    /// <param name="slug">
    /// The content's slug. Empty is legitimate - it is what a page carrying a single content uses - and
    /// yields an empty path, so the content's URL is the page route alone.
    /// </param>
    public static string Build(string? pattern, DateTime publishTime, string? slug)
    {
        if (string.IsNullOrEmpty(slug))
        {
            return string.Empty;
        }

        var normalized = Normalize(pattern);

        // Invariant culture: these segments are URL structure, not text shown to anybody, so they must
        // not vary with the culture the request happens to run under. A Buddhist or Hijri calendar on
        // the ambient culture would otherwise silently emit a different year.
        var built = PublishTimePlaceholder.Replace(
            normalized,
            match => publishTime.ToString(match.Groups[1].Value, CultureInfo.InvariantCulture));

        return built.Replace(SlugToken, slug, StringComparison.Ordinal).Trim('/');
    }

    /// <summary>
    /// The reverse of <see cref="Build"/>: reads the slug back out of the part of a request path that
    /// follows the page route.
    /// <para>
    /// Only the slug is returned, because only the slug identifies the content -
    /// <c>(PageId, CultureName, Slug)</c> is the unique constraint, so any date segment is decoration
    /// that has already done its job by matching. Requiring it to agree with the content's stored
    /// publish time would mean an editor who reschedules a post breaks every link to it.
    /// </para>
    /// </summary>
    /// <param name="pattern">The owning page's pattern; null/blank means <see cref="Default"/>.</param>
    /// <param name="path">The remainder of the request path after the page route, without leading slash.</param>
    public static bool TryExtractSlug(string? pattern, string? path, out string slug)
    {
        slug = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var match = ToRegex(Normalize(pattern)).Match(path.Trim('/'));
        if (!match.Success)
        {
            return false;
        }

        slug = match.Groups["slug"].Value;
        return slug.Length > 0;
    }

    /// <summary>
    /// Compiles a pattern into an anchored regex. Everything outside a placeholder is escaped, so a
    /// pattern containing regex metacharacters is matched literally rather than reinterpreted.
    /// </summary>
    private static Regex ToRegex(string pattern)
    {
        var builder = new StringBuilder("^");
        var cursor = 0;

        foreach (Match placeholder in PublishTimePlaceholder.Matches(pattern))
        {
            builder.Append(EscapeWithSlugToken(pattern[cursor..placeholder.Index]));
            builder.Append(DateFormatToRegex(placeholder.Groups[1].Value));
            cursor = placeholder.Index + placeholder.Length;
        }

        builder.Append(EscapeWithSlugToken(pattern[cursor..]));
        builder.Append('$');

        return new Regex(builder.ToString(), RegexOptions.None);
    }

    /// <summary>
    /// Escapes a literal run, then re-substitutes the slug capture - <c>{slug}</c> survives
    /// <see cref="Regex.Escape"/> as <c>\{slug}</c>, so both spellings have to be replaced.
    /// </summary>
    private static string EscapeWithSlugToken(string literal)
    {
        return Regex.Escape(literal)
            .Replace(@"\{slug}", "(?<slug>[^/]+)", StringComparison.Ordinal)
            .Replace(SlugToken, "(?<slug>[^/]+)", StringComparison.Ordinal);
    }

    /// <summary>
    /// Translates a date format string into a regex that matches what
    /// <see cref="DateTime.ToString(string, IFormatProvider)"/> would have produced for it.
    /// </summary>
    private static string DateFormatToRegex(string format)
    {
        var builder = new StringBuilder();
        var index = 0;

        while (index < format.Length)
        {
            var matched = false;

            foreach (var (specifier, regex) in DateSpecifiers)
            {
                if (string.CompareOrdinal(format, index, specifier, 0, specifier.Length) == 0)
                {
                    builder.Append(regex);
                    index += specifier.Length;
                    matched = true;
                    break;
                }
            }

            if (!matched)
            {
                builder.Append(Regex.Escape(format[index].ToString()));
                index++;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// The placeholder names a pattern may use - surfaced so an admin UI or an MCP tool description can
    /// list them instead of hard-coding its own copy.
    /// </summary>
    public static IReadOnlyList<string> SupportedPlaceholders { get; } = new[]
    {
        SlugToken,
        "{publishTime:yyyy}",
        "{publishTime:yyyy/MM}",
        "{publishTime:yyyy/MM/dd}"
    };
}
