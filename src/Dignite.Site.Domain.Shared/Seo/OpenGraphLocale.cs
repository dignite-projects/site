using System;
using System.Globalization;

namespace Dignite.Site.Seo;

/// <summary>
/// A language as Open Graph writes it in <c>og:locale</c>: <c>language_TERRITORY</c>, such as <c>ja_JP</c> or
/// <c>zh_CN</c>. A site stores languages as BCP 47 names (<c>ja</c>, <c>zh-Hans</c>), which name no territory, so
/// the territory is the one the runtime pairs the language with (<see cref="CultureInfo.CreateSpecificCulture"/>):
/// <c>en</c> is <c>en_US</c>, <c>zh-Hans</c> is <c>zh_CN</c>, <c>zh-Hant</c> is <c>zh_TW</c>. A script subtag is
/// dropped, as Open Graph has no place for it.
/// </summary>
public static class OpenGraphLocale
{
    /// <summary>
    /// <paramref name="cultureName"/> as an <c>og:locale</c> value; the language alone when the runtime knows no
    /// territory for it (or runs with invariant globalization); null for a blank or unknown name.
    /// </summary>
    public static string? From(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return null;
        }

        var language = cultureName.Split('-')[0].ToLowerInvariant();

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
