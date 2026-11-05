using System;
using System.Globalization;
using System.Linq;

namespace Dignite.Site.Seo;

/// <summary>
/// A language as Open Graph writes it in <c>og:locale</c>: <c>language_TERRITORY</c>, such as <c>ja_JP</c> or
/// <c>zh_CN</c>. A site stores languages as BCP 47 names (<c>ja</c>, <c>zh-Hans</c>), which mostly name no territory:
/// <list type="bullet">
/// <item>A name with a territory keeps it: <c>en-GB</c> is <c>en_GB</c>, <c>zh-Hant-HK</c> is <c>zh_HK</c>.</item>
/// <item>Chinese without one takes it from its script: <c>zh-Hans</c> (and <c>zh</c>) is <c>zh_CN</c>, <c>zh-Hant</c> is
/// <c>zh_TW</c>. The runtime is not asked here: on Windows it pairs <c>zh-Hant</c> with Hong Kong, and what it answers
/// can differ from one platform's ICU to another's, while a page should say the same on every host.</item>
/// <item>Any other language takes the territory the runtime pairs it with
/// (<see cref="CultureInfo.CreateSpecificCulture"/>): <c>en</c> is <c>en_US</c>, <c>ja</c> is <c>ja_JP</c>.</item>
/// </list>
/// A script subtag is dropped, as Open Graph has no place for it.
/// </summary>
public static class OpenGraphLocale
{
    /// <summary>
    /// <paramref name="cultureName"/> as an <c>og:locale</c> value; the language alone when no territory can be found
    /// for it (or the runtime runs with invariant globalization); null for a blank or unknown name.
    /// </summary>
    public static string? From(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return null;
        }

        var subtags = cultureName.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries);
        var language = subtags[0].ToLowerInvariant();

        // The territory a name gives itself: two letters (GB) or three digits (419), after the language and its script.
        var region = subtags.Skip(1).FirstOrDefault(s =>
            (s.Length == 2 && s.All(char.IsAsciiLetter)) || (s.Length == 3 && s.All(char.IsAsciiDigit)));
        if (region != null)
        {
            return $"{language}_{region.ToUpperInvariant()}";
        }

        if (language == "zh")
        {
            var script = subtags.Skip(1).FirstOrDefault(s => s.Length == 4);
            return string.Equals(script, "Hant", StringComparison.OrdinalIgnoreCase) ? "zh_TW" : "zh_CN";
        }

        CultureInfo specific;
        try
        {
            specific = CultureInfo.CreateSpecificCulture(cultureName);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }

        // Invariant globalization answers every name with the invariant culture, whose name is empty.
        if (string.IsNullOrEmpty(specific.Name) || specific.IsNeutralCulture)
        {
            return language;
        }

        try
        {
            return $"{language}_{new RegionInfo(specific.Name).TwoLetterISORegionName}";
        }
        catch (ArgumentException)
        {
            return language;
        }
    }
}
