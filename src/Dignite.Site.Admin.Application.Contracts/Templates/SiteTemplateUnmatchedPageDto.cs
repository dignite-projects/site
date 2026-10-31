using System;

namespace Dignite.Site.Admin.Templates;

/// <summary>
/// A routable page whose <c>Template</c> names no published template, so the site renders it with the
/// Razor view of that name - see <see cref="ISiteTemplateAdminAppService.GetUnmatchedPagesAsync"/>.
/// </summary>
public class SiteTemplateUnmatchedPageDto
{
    public Guid PageId { get; set; }

    public string Name { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    /// <summary>The page's <c>Template</c>, as stored.</summary>
    public string Template { get; set; } = default!;

    /// <summary>The template of that name when one exists but was never published; null when there is none.</summary>
    public Guid? TemplateId { get; set; }

    /// <summary>Whether a template of that name exists but was never published - publishing it takes the page over.</summary>
    public bool HasDraft { get; set; }
}
