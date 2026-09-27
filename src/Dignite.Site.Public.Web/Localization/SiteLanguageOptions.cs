using System.Collections.Generic;

namespace Dignite.Site.Public.Localization;

/// <summary>
/// How the rendering side applies the site's languages (GitHub issue #75): which in-site links get a
/// language prefix, and whether <c>/</c> sends a visitor to their own language's home page.
/// </summary>
public class SiteLanguageOptions
{
    /// <summary>
    /// Paths that are not Site pages, so an in-site link to them never gets a language prefix
    /// (<see cref="SiteLinks"/>): prefixed, they would 404. By default ABP's and ASP.NET Core's own endpoints:
    /// the account pages, ABP's scripts and APIs, OpenIddict (<c>/connect</c>), Swagger, the error page and
    /// <c>/.well-known</c>. Matched per whole segment, ignoring case, with or without slashes - "api" covers
    /// "/api/..." but not "/apiary". Anything whose last segment contains a dot (<c>favicon.ico</c>,
    /// <c>sitemap.xml</c>) is excluded as well, without being listed here. A host that serves its own pages
    /// next to Site's - a contact form under <c>/Contact</c>, say - adds their paths.
    /// </summary>
    public List<string> ExcludedPathPrefixes { get; } = new()
    {
        "/Account", "/Abp", "/api", "/connect", "/swagger", "/Error", "/.well-known"
    };

    /// <summary>
    /// Whether a visitor arriving at <c>/</c> is sent (302) to their own language's home page - the language
    /// named by <see cref="CookieName"/>, or failing that the best match for the browser's
    /// <c>Accept-Language</c>. Off by default: it is a policy, not a rule, and some sites want <c>/</c> to
    /// stay put for everyone. See <see cref="SiteHomeLanguageRedirectFilter"/>.
    /// </summary>
    public bool RedirectHomeToVisitorLanguage { get; set; }

    /// <summary>
    /// The cookie holding a visitor's explicit language choice, which outranks <c>Accept-Language</c> for the
    /// <c>/</c> redirect. Site only reads it; the host's language switcher writes it (a culture name, e.g.
    /// <c>ja</c>) when a visitor picks a language.
    /// </summary>
    public string CookieName { get; set; } = "site-lang";
}
