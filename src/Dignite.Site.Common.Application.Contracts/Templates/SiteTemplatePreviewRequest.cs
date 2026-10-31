namespace Dignite.Site.Templates;

/// <summary>What <see cref="ISiteTemplatePreviewRenderer"/> renders: a version of a template, on a page of the site.</summary>
public class SiteTemplatePreviewRequest
{
    /// <summary>
    /// The template's name as stored (normalized, <c>SiteTemplate.NormalizeName</c>): the value a page's
    /// <c>Template</c> holds, <c>layout</c> (<see cref="SiteTemplateConsts.LayoutName"/>) for the template
    /// that wraps every page, or a <c>partials/</c> or <c>assets/</c> name, which is substituted where the page's
    /// templates include it or point at it.
    /// </summary>
    public string TemplateName { get; set; } = default!;

    /// <summary>The version rendered in place of the published one - a draft, the published one or an older one.</summary>
    public SiteTemplateVersionDto Version { get; set; } = default!;

    /// <summary>
    /// The site path to render, as a visitor would request it: <c>/about</c>, <c>/ja/blog/hello</c>, a
    /// language prefix included.
    /// </summary>
    public string Path { get; set; } = default!;
}
