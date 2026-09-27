using Dignite.Site.Seo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dignite.Site.Public.Localization;

public static class SiteUrlHelperExtensions
{
    /// <summary>
    /// <see cref="IUrlHelper.Content"/>, in the current request's language (GitHub issue #75):
    /// <c>Url.SiteContent("~/about")</c> is <c>/about</c> on a default-language page and <c>/ja/about</c> on
    /// a Japanese one - see <see cref="SiteLinks.Localize"/>. For paths a view builds in code, such as a menu
    /// item's URL; a literal <c>&lt;a href="~/..."&gt;</c> is localized by
    /// <see cref="TagHelpers.SiteLinkTagHelper"/> already.
    /// <para>
    /// Synchronous, so it relies on the languages having been loaded before the view rendered
    /// (<see cref="SiteLanguageResultFilter"/>); called anywhere else it throws - see
    /// <see cref="SiteLanguageContext.GetLoadedLanguages"/>.
    /// </para>
    /// </summary>
    public static string SiteContent(this IUrlHelper url, string contentPath)
    {
        var languageContext = GetLanguageContext(url);
        var languages = languageContext.GetLoadedLanguages();

        return Localize(url, contentPath, languageContext.GetCurrentCultureName(languages), languages);
    }

    /// <summary>
    /// <see cref="IUrlHelper.Content"/>, in <paramref name="cultureName"/> rather than the current request's
    /// language - e.g. a link to a page in one language in particular.
    /// </summary>
    public static string SiteContent(this IUrlHelper url, string contentPath, string cultureName)
    {
        return Localize(url, contentPath, cultureName, GetLanguageContext(url).GetLoadedLanguages());
    }

    private static string Localize(IUrlHelper url, string contentPath, string cultureName, SiteLanguages languages)
    {
        var options = url.ActionContext.HttpContext.RequestServices.GetRequiredService<IOptions<SiteLanguageOptions>>();

        return url.Content(SiteLinks.Localize(contentPath, cultureName, languages, options.Value.ExcludedPathPrefixes));
    }

    private static SiteLanguageContext GetLanguageContext(IUrlHelper url)
    {
        return url.ActionContext.HttpContext.RequestServices.GetRequiredService<SiteLanguageContext>();
    }
}
