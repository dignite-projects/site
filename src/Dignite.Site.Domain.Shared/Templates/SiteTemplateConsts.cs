using Dignite.Site.Pages;

namespace Dignite.Site.Templates;

/// <summary>
/// Limits and well-known names for <c>SiteTemplate</c>. Mutable statics (like <see cref="PageConsts"/>) so a
/// host can widen a limit before its model is built.
/// </summary>
public static class SiteTemplateConsts
{
    /// <summary>
    /// Same as <see cref="PageConsts.MaxTemplateLength"/>, because a template is looked up by the value
    /// <c>Page.Template</c> holds. A property rather than a copied number, so the two cannot drift apart;
    /// a static property (not a const) so <c>DynamicStringLength</c> can read it.
    /// </summary>
    public static int MaxNameLength => PageConsts.MaxTemplateLength;

    /// <summary>
    /// The shape of <see cref="PageConsts.TemplatePattern"/> - letters, digits, <c>_</c>, <c>-</c>, grouped with
    /// <c>/</c> - so a page template is a name a page can hold, and <c>..</c> cannot be spelled. One extension:
    /// a name under <see cref="AssetsPrefix"/> may contain <c>.</c> (<c>assets/site.css</c>), because an asset is
    /// served with a <c>Content-Type</c> taken from its extension. Still no <c>..</c> anywhere and no <c>.</c>
    /// right after a <c>/</c>, so neither path traversal nor a hidden-file name can be spelled under <c>assets/</c>
    /// either. A page template with a dot would be unreachable (no page can name it), so dots stay out there.
    /// </summary>
    public const string NamePattern =
        @"^\/?(?!.*\.\.)(?!.*\/\.)(?:" + AssetsPrefix + @"[A-Za-z0-9_][A-Za-z0-9_\-\/.]*|[A-Za-z0-9_][A-Za-z0-9_\-\/]*)$";

    /// <summary>Default value: 256 KiB of template source per version.</summary>
    public static int MaxBodyLength { get; set; } = 256 * 1024;

    /// <summary>Default value: 256. The note that says what a version changed.</summary>
    public static int MaxCommentLength { get; set; } = 256;

    /// <summary>
    /// The template that wraps every page's body. It must call <see cref="SiteHeadFunctionName"/>, or the
    /// pages rendered through it would ship without their SEO head tags.
    /// </summary>
    public const string LayoutName = "layout";

    /// <summary>The variable a layout reads to place the rendered page template.</summary>
    public const string BodyVariableName = "body";

    /// <summary>The function that emits the page's SEO head tags (title, meta, canonical, hreflang, ...).</summary>
    public const string SiteHeadFunctionName = "site_head";

    /// <summary>
    /// First segment of the templates that <c>include</c> may pull in, and the only ones it may: see
    /// <see cref="SiteTemplateNames"/>.
    /// </summary>
    public const string PartialsPrefix = "partials/";

    /// <summary>
    /// First segment of the text assets (CSS, JS, ...) served as they are by <c>GET /site-assets/</c>; their
    /// bodies are not Scriban: see <see cref="SiteTemplateNames"/>.
    /// </summary>
    public const string AssetsPrefix = "assets/";
}
