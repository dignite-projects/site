using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;

namespace Dignite.Site.Public.Templating;

/// <summary>
/// A text a template looks up, or the template's own fallback when the site has not written it - e.g. a
/// Select option's name, falling back to the option's Text: <c>@L.GetOrDefault($"Field:blog_category:{option.Value}", option.Text)</c>.
/// <para>
/// A tenant's texts never fall back to the host's (GitHub issue #73), so this is how a template covers a
/// text a site has not translated, instead of showing the key. Which keys exist, and how a value maps to
/// one, is the template's own choice - nothing here knows a naming convention.
/// </para>
/// </summary>
public static class LocalizerTemplateExtensions
{
    /// <summary>The text named <paramref name="name"/>, or <paramref name="fallback"/> - or, when that is null, the name itself - if there is none.</summary>
    public static string GetOrDefault(this IStringLocalizer localizer, string name, string? fallback)
    {
        var text = localizer[name];
        return text.ResourceNotFound ? fallback ?? name : text.Value;
    }

    /// <summary>
    /// The HTML-localizer form, for <c>IHtmlLocalizer&lt;T&gt;</c> and <c>IViewLocalizer</c>. A found text is
    /// written as the localizer writes it; <paramref name="fallback"/> is usually data (an option's Text), so
    /// it is HTML-encoded.
    /// </summary>
    public static IHtmlContent GetOrDefault(this IHtmlLocalizer localizer, string name, string? fallback)
    {
        var text = localizer[name];
        return text.IsResourceNotFound
            ? new HtmlContentBuilder().Append(fallback ?? name)
            : text;
    }
}
