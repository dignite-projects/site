using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dignite.Site.Seo;
using Microsoft.Net.Http.Headers;

namespace Dignite.Site.Public.Localization;

/// <summary>
/// Maps a visitor's languages - a browser's <c>Accept-Language</c>, a remembered choice - onto the languages
/// a site serves (GitHub issue #75).
/// </summary>
public static class SiteLanguageMatcher
{
    /// <summary>
    /// <paramref name="cultureName"/> in its normalized form, if the site serves exactly that language;
    /// otherwise null. For a value the site itself handed out, such as a switcher's cookie.
    /// </summary>
    public static string? FindServed(SiteLanguages languages, string? cultureName)
    {
        return CultureNameNormalizer.TryNormalize(cultureName, out var normalized) && languages.IsServed(normalized)
            ? normalized
            : null;
    }

    /// <summary>
    /// The site language that best serves <paramref name="languageTag"/> (a BCP 47 tag, as a browser sends
    /// it), or null when none fits. The tag's own culture is tried first, then its .NET parent chain - which
    /// is where <c>zh-TW</c>, <c>zh-HK</c> and <c>zh-MO</c> lead to <c>zh-Hant</c>, and <c>zh-CN</c>,
    /// <c>zh-SG</c> to <c>zh-Hans</c>. Failing that, the first language in configured order that shares the
    /// tag's root: <c>en-GB</c> for <c>en-US</c>, any Chinese for a bare <c>zh</c>.
    /// <para>
    /// The root keeps the script when the tag has one, so the fallback never crosses scripts: <c>zh-TW</c>'s
    /// root is <c>zh-Hant</c>, not <c>zh</c>, and a site serving only <c>zh-Hans</c> is no match for it - a
    /// reader of Traditional Chinese is better served by the default language than by Simplified. The same
    /// holds for <c>sr-Latn</c> and <c>sr-Cyrl</c>.
    /// </para>
    /// </summary>
    public static string? Match(SiteLanguages languages, string? languageTag)
    {
        if (!TryGetCulture(languageTag, out var culture))
        {
            return null;
        }

        var chain = CultureChain.Of(culture).ToList();

        foreach (var member in chain)
        {
            if (FindServed(languages, member.Name) is { } exact)
            {
                return exact;
            }
        }

        var root = (chain.LastOrDefault(member => HasScript(member.Name)) ?? chain[^1]).Name;
        return languages.EnabledCultureNames.FirstOrDefault(cultureName =>
            TryGetCulture(cultureName, out var enabled) && CultureChain.Of(enabled).Any(member => member.Name == root));
    }

    /// <summary>
    /// The site language that best serves an <c>Accept-Language</c> header: its tags by descending quality,
    /// each through <see cref="Match(SiteLanguages, string?)"/>, first hit wins. Tags of quality 0 ("not
    /// acceptable") are skipped.
    /// </summary>
    public static string? Match(SiteLanguages languages, IEnumerable<StringWithQualityHeaderValue> acceptLanguage)
    {
        // OrderByDescending is stable, so equal-quality tags keep the browser's own order.
        return acceptLanguage
            .Where(value => (value.Quality ?? 1) > 0)
            .OrderByDescending(value => value.Quality ?? 1)
            .Select(value => Match(languages, value.Value.Value))
            .FirstOrDefault(cultureName => cultureName != null);
    }

    /// <summary>Whether a culture name carries a script subtag - four letters, as <c>Hant</c> in <c>zh-Hant</c>.</summary>
    private static bool HasScript(string cultureName)
    {
        return cultureName.Split('-').Skip(1).Any(subtag => subtag.Length == 4 && subtag.All(char.IsAsciiLetter));
    }

    private static bool TryGetCulture(string? name, out CultureInfo culture)
    {
        culture = CultureInfo.InvariantCulture;

        // The same predefinedOnly rule as CultureNameNormalizer: "*" or a made-up tag matches nothing.
        if (!CultureNameNormalizer.TryNormalize(name, out var normalized))
        {
            return false;
        }

        culture = CultureInfo.GetCultureInfo(normalized);
        return !string.IsNullOrEmpty(culture.Name);
    }
}
