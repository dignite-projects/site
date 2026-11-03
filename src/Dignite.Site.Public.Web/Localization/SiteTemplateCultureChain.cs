using System.Collections.Generic;
using System.Globalization;

namespace Dignite.Site.Public.Localization;

/// <summary>
/// The languages a template text is looked up in, most specific first: the language and all its parents, then
/// the site's default language and its parents, without repeats. <c>zh-Hant-TW</c> with default <c>en-GB</c> gives
/// <c>zh-Hant-TW, zh-Hant, zh, en-GB, en</c>.
/// <para>
/// One rule for both places template texts come from: the host's files, which
/// <see cref="SiteTemplateLocalizationContributor"/> serves to Razor views, and a Scriban theme's own
/// (<see cref="SiteThemeTextCatalog"/>), which <c>t</c> reads. It lives here rather than in ABP's own fallback,
/// which climbs one parent level at most and has no notion of a per-site default.
/// </para>
/// </summary>
public static class SiteTemplateCultureChain
{
    public static IReadOnlyList<string> Create(string cultureName, string? defaultCultureName)
    {
        var chain = new List<string>();
        AddWithParents(chain, cultureName);

        if (!string.IsNullOrWhiteSpace(defaultCultureName))
        {
            AddWithParents(chain, defaultCultureName);
        }

        return chain;
    }

    private static void AddWithParents(List<string> chain, string cultureName)
    {
        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            if (!chain.Contains(cultureName))
            {
                chain.Add(cultureName);
            }

            return;
        }

        foreach (var member in CultureChain.Of(culture))
        {
            if (!chain.Contains(member.Name))
            {
                chain.Add(member.Name);
            }
        }
    }
}
