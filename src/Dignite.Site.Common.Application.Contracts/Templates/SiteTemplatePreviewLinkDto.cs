using System;

namespace Dignite.Site.Templates;

/// <summary>A signed link that opens the site with a template version in place of the published one.</summary>
public class SiteTemplatePreviewLinkDto
{
    /// <summary>The absolute address to open: the path on the site, with the preview token as a query parameter.</summary>
    public string Url { get; set; } = default!;

    /// <summary>The site path the link opens, normalized to start with a slash.</summary>
    public string Path { get; set; } = default!;

    /// <summary>The template the link previews, as stored.</summary>
    public string TemplateName { get; set; } = default!;

    /// <summary>The number of the version the link previews.</summary>
    public int VersionNumber { get; set; }

    /// <summary>When the token in <see cref="Url"/> stops being accepted. The preview cookie it is exchanged for lives longer.</summary>
    public DateTime ExpiresAt { get; set; }
}
