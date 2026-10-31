using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Volo.Abp;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// The bar at the top of every page rendered in draft mode: which version is shown, and the way out. Styled inline,
/// so it shows the same on any tenant's markup and needs no stylesheet. Sticky rather than fixed: as the first
/// child of <c>&lt;body&gt;</c> it stays in the flow, so it pushes the page's own top bar down instead of covering
/// it, and still stays in view while scrolling. A page whose own header is fixed to the top of the viewport may
/// cover it, which is the trade-off taken.
/// </summary>
public static class SiteTemplatePreviewBanner
{
    private static readonly Regex BodyStartTag = new(
        "<body(\\s[^>]*)?>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// The banner's markup, every text and the address HTML-encoded.
    /// </summary>
    /// <param name="text">What is previewed - <c>TemplatePreviewBanner</c>, already formatted.</param>
    /// <param name="exitText">The exit link's text - <c>ExitTemplatePreview</c>.</param>
    /// <param name="exitUrl">The address of <see cref="SiteTemplatePreviewController"/>'s exit, with its <c>returnUrl</c>.</param>
    /// <param name="encoder">The encoder; <see cref="HtmlEncoder.Default"/> when null.</param>
    public static string Build(string text, string exitText, string exitUrl, HtmlEncoder? encoder = null)
    {
        Check.NotNull(text, nameof(text));
        Check.NotNull(exitText, nameof(exitText));
        Check.NotNull(exitUrl, nameof(exitUrl));

        encoder ??= HtmlEncoder.Default;

        return "<div id=\"" + SiteTemplatePreviewConsts.BannerElementId + "\" style=\"position:sticky;top:0;" +
               "z-index:2147483647;box-sizing:border-box;padding:8px 16px;background:#1f2937;color:#f9fafb;" +
               "font:14px/1.4 system-ui,sans-serif;display:flex;gap:16px;align-items:center;justify-content:center;" +
               "box-shadow:0 1px 4px rgba(0,0,0,.4)\"><span>" + encoder.Encode(text) + "</span><a href=\"" +
               encoder.Encode(exitUrl) + "\" style=\"color:#fff;text-decoration:underline\">" + encoder.Encode(exitText) +
               "</a></div>";
    }

    /// <summary>
    /// <paramref name="html"/> with <paramref name="banner"/> right after its opening <c>&lt;body&gt;</c> tag. A
    /// document without one gets the banner at its very end - the parser moves it into the body, where it sits below
    /// the page's content; at the very start it would come before the doctype and switch the page to quirks mode.
    /// <para>
    /// Spliced in by index, not with <see cref="Regex.Replace(string, string)"/>: a replacement string interprets
    /// <c>$1</c> and the like, and neither the banner nor the page is ever a pattern.
    /// </para>
    /// </summary>
    public static string Inject(string html, string banner)
    {
        Check.NotNull(html, nameof(html));
        Check.NotNull(banner, nameof(banner));

        var match = BodyStartTag.Match(html);
        if (!match.Success)
        {
            return html + banner;
        }

        var index = match.Index + match.Length;
        return html[..index] + banner + html[index..];
    }
}
