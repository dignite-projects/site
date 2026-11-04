using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace Dignite.Site.Admin.Themes;

/// <summary>
/// The references in a rendered page to an id on that same page that nothing there has: a <c>&lt;use href="#icon"&gt;</c> whose
/// symbol is not in the page (the icon shows nothing), a <c>fill="url(#gradient)"</c> with no gradient, an anchor link to a
/// section that does not exist. They cannot be found in a template - the id may be defined by a layout or by another
/// partial, which only the rendered page puts together - and nothing fails when they are wrong, so the page just looks
/// broken: the trial render reads what it rendered for them.
/// </summary>
public static class RenderedFragmentReferences
{
    private static readonly Regex Ids = new(
        """\bid\s*=\s*(?:"([^"]*)"|'([^']*)')""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // href="#x" (and xlink:href, whose word boundary is the colon) and url(#x). A reference to another document
    // (href="other.svg#x") starts with something else and is not matched; href="#" alone is nothing.
    private static readonly Regex References = new(
        """\bhref\s*=\s*(?:"#([^"\s]+)"|'#([^'\s]+)')|\burl\(\s*#([^)\s"']+)\s*\)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// The ids <paramref name="html"/> refers to by <c>#id</c> and does not define, each once, in the order they are first
    /// referred to. <c>#top</c> is left out: a browser scrolls to the top for it whether or not an element has that id.
    /// </summary>
    public static IReadOnlyList<string> FindMissing(string? html)
    {
        var missing = new List<string>();
        if (string.IsNullOrEmpty(html))
        {
            return missing;
        }

        var defined = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Ids.Matches(html))
        {
            defined.Add(Decode(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value));
        }

        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in References.Matches(html))
        {
            var id = Decode(match.Groups[1].Success ? match.Groups[1].Value
                : match.Groups[2].Success ? match.Groups[2].Value
                : match.Groups[3].Value);

            if (id.Length == 0 || id.Equals("top", StringComparison.OrdinalIgnoreCase) || defined.Contains(id) || !reported.Add(id))
            {
                continue;
            }

            missing.Add(id);
        }

        return missing;
    }

    private static string Decode(string value)
    {
        try
        {
            return Uri.UnescapeDataString(WebUtility.HtmlDecode(value));
        }
        catch (UriFormatException)
        {
            return value;
        }
    }
}
