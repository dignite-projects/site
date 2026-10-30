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
    /// Same shape as <see cref="PageConsts.TemplatePattern"/> for the same reason as
    /// <see cref="MaxNameLength"/>: a name that a page could not hold is a name no page could ever use.
    /// </summary>
    public const string NamePattern = PageConsts.TemplatePattern;

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
}
