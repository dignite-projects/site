namespace Dignite.Site.Templates;

/// <summary>A page of the site rendered with a template version in place of the published one.</summary>
public class SiteTemplatePreviewDto
{
    /// <summary>The page's HTML, as it would be served.</summary>
    public string Html { get; set; } = default!;

    /// <summary>The path that was rendered, normalized to start with a slash.</summary>
    public string Path { get; set; } = default!;

    /// <summary>The name of the page the path resolved to.</summary>
    public string PageName { get; set; } = default!;

    /// <summary>The language the path resolved to, which the page was rendered in.</summary>
    public string CultureName { get; set; } = default!;

    /// <summary>
    /// The matched page's own <c>Template</c>. When it differs from the previewed template's name (and that is
    /// not <c>layout</c>), the page does not normally render through the previewed template - the preview shows
    /// what it would look like if it did.
    /// </summary>
    public string PageTemplate { get; set; } = default!;

    /// <summary>The number of the version that was rendered.</summary>
    public int VersionNumber { get; set; }
}
