using Dignite.Site.Templates;

namespace Dignite.Site.Public.Templates;

/// <summary>
/// Everything the renderer needs to turn one page view into HTML: the published template of the requested
/// name and the published layout around it.
/// </summary>
public class SiteTemplateRenderSetDto
{
    /// <summary>The published version of the requested template, or null when it is missing or unpublished.</summary>
    public SiteTemplateVersionDto? Template { get; set; }

    /// <summary>The published version of the template named <c>layout</c>, or null when there is none.</summary>
    public SiteTemplateVersionDto? Layout { get; set; }
}
