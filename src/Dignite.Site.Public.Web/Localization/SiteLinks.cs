using System;
using System.Collections.Generic;
using System.Linq;
using Dignite.Site.Seo;
using Microsoft.AspNetCore.Http;

namespace Dignite.Site.Public.Localization;

/// <summary>
/// Keeps in-site links in the language of the page they appear on (GitHub issue #75): on <c>/ja/about</c>,
/// <c>~/services</c> has to mean <c>~/ja/services</c>, or every link leads back to the default language.
/// Applied by <see cref="TagHelpers.SiteLinkTagHelper"/> to every in-site link a template writes as
/// <c>~/...</c> (<c>&lt;a href&gt;</c>, <c>&lt;form action&gt;</c>, ...), and by
/// <see cref="SiteUrlHelperExtensions.SiteContent(Microsoft.AspNetCore.Mvc.IUrlHelper, string)"/> wherever a
/// view or menu builds a path in code. The prefix itself is <see cref="SiteLanguages.ApplyCulturePrefix"/> -
/// the very rule routing strips it with.
/// </summary>
public static class SiteLinks
{
    /// <summary>
    /// Puts <paramref name="cultureName"/>'s prefix on an app-relative URL: <c>~/about?tab=2#team</c> becomes
    /// <c>~/ja/about?tab=2#team</c>. Returned unchanged when there is nothing to do - the default language, a
    /// language the site does not serve, a URL that is not app-relative (<c>~/</c>), one that already carries
    /// a language prefix, or one that is not a Site page (<see cref="IsSitePagePath"/>).
    /// </summary>
    public static string Localize(
        string url,
        string cultureName,
        SiteLanguages languages,
        IEnumerable<string> excludedPathPrefixes)
    {
        if (!url.StartsWith("~/", StringComparison.Ordinal)
            || !CultureNameNormalizer.TryNormalize(cultureName, out var normalizedCulture)
            || normalizedCulture == languages.DefaultCultureName
            || !languages.IsServed(normalizedCulture))
        {
            return url;
        }

        var suffixStart = url.IndexOfAny(['?', '#']);
        var path = suffixStart < 0 ? url[1..] : url[1..suffixStart];
        var suffix = suffixStart < 0 ? string.Empty : url[suffixStart..];

        // Already prefixed - "~/en/..." written on purpose, to link to one language in particular.
        if (!IsSitePagePath(path, excludedPathPrefixes) || languages.TryStripCulturePrefix(path, out _, out _))
        {
            return url;
        }

        return "~" + languages.ApplyCulturePrefix(path, normalizedCulture) + suffix;
    }

    /// <summary>
    /// Whether <paramref name="path"/> is served by Site's catch-all page route, and so exists under every
    /// language prefix. The <paramref name="excludedPathPrefixes"/> (ABP's own endpoints by default -
    /// <see cref="SiteLanguageOptions.ExcludedPathPrefixes"/>) are not, and neither is anything file-like
    /// (<c>favicon.ico</c>): prefixed, they 404. A prefix is matched per whole segment, ignoring case, with or
    /// without its leading and trailing slashes - <c>"api"</c>, <c>"/api"</c> and <c>"/api/"</c> all cover
    /// <c>/api/...</c> but not <c>/apiary</c>.
    /// </summary>
    public static bool IsSitePagePath(string path, IEnumerable<string> excludedPathPrefixes)
    {
        var pathString = new PathString(path.StartsWith('/') ? path : "/" + path);

        if (excludedPathPrefixes.Any(prefix => IsUnder(pathString, prefix)))
        {
            return false;
        }

        var lastSegment = path[(path.LastIndexOf('/') + 1)..];
        return !lastSegment.Contains('.');
    }

    private static bool IsUnder(PathString path, string prefix)
    {
        var normalized = "/" + prefix.Trim().Trim('/');

        // "/" alone would cover every path; PathString.StartsWithSegments does the whole-segment rest.
        return normalized.Length > 1 && path.StartsWithSegments(normalized, StringComparison.OrdinalIgnoreCase);
    }
}
