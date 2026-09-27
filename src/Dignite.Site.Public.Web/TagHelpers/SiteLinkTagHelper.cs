using System;
using System.Net;
using System.Threading.Tasks;
using Dignite.Site.Public.Localization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Options;

namespace Dignite.Site.Public.TagHelpers;

/// <summary>
/// Rewrites in-site links written as <c>~/...</c> into the current page's language (GitHub issue #75), so
/// templates keep writing plain app-relative links: on <c>/ja/about</c>, <c>href="~/services"</c> becomes
/// <c>/ja/services</c> - see <see cref="SiteLinks.Localize"/> for what is left alone. Every attribute that
/// navigates: <c>&lt;a href&gt;</c>, <c>&lt;area href&gt;</c>, <c>&lt;form action&gt;</c>, and
/// <c>formaction</c> on <c>&lt;button&gt;</c> and <c>&lt;input&gt;</c>. On a single-language site every page
/// is in the default language, so it never changes a link.
/// <para>
/// On wherever <c>@addTagHelper *, Dignite.Site.Public.Web</c> is. It runs before Razor's built-in
/// <c>UrlResolutionTagHelper</c> (Order -1999), which then resolves the rewritten <c>~/ja/...</c> against
/// PathBase as usual. A link Razor has already resolved - <c>href="@Url.Content("~/about")"</c> - no longer
/// starts with <c>~/</c> and is not touched, which is the way to link to the default language on purpose.
/// </para>
/// </summary>
[HtmlTargetElement("a", Attributes = HrefAttributeName)]
[HtmlTargetElement("area", Attributes = HrefAttributeName)]
[HtmlTargetElement("form", Attributes = ActionAttributeName)]
[HtmlTargetElement("button", Attributes = FormActionAttributeName)]
[HtmlTargetElement("input", Attributes = FormActionAttributeName)]
public class SiteLinkTagHelper : TagHelper
{
    private const string HrefAttributeName = "href";
    private const string ActionAttributeName = "action";
    private const string FormActionAttributeName = "formaction";

    protected SiteLanguageContext LanguageContext { get; }
    protected IOptions<SiteLanguageOptions> Options { get; }

    public SiteLinkTagHelper(SiteLanguageContext languageContext, IOptions<SiteLanguageOptions> options)
    {
        LanguageContext = languageContext;
        Options = options;
    }

    public override int Order => -3000;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var attributeName = GetUrlAttributeName(context.TagName);
        if (!output.Attributes.TryGetAttribute(attributeName, out var attribute))
        {
            return;
        }

        // Razor hands a literal - and a value mixing literal text and code, "~/blog/@slug" - over as an
        // HtmlString that is already HTML-encoded; decoded here, so an encoded "&#x...;" character is not
        // mistaken for a "#fragment", and set back as a plain string that the output encodes again.
        var url = attribute.Value switch
        {
            string value => value,
            HtmlString value => WebUtility.HtmlDecode(value.Value),
            _ => null
        };

        if (url == null || !url.StartsWith("~/", StringComparison.Ordinal))
        {
            return;
        }

        var languages = await LanguageContext.GetLanguagesAsync();
        var localized = SiteLinks.Localize(
            url, LanguageContext.GetCurrentCultureName(languages), languages, Options.Value.ExcludedPathPrefixes);

        if (!string.Equals(localized, url, StringComparison.Ordinal))
        {
            output.Attributes.SetAttribute(attributeName, localized);
        }
    }

    private static string GetUrlAttributeName(string tagName)
    {
        return tagName.ToLowerInvariant() switch
        {
            "form" => ActionAttributeName,
            "button" or "input" => FormActionAttributeName,
            _ => HrefAttributeName
        };
    }
}
