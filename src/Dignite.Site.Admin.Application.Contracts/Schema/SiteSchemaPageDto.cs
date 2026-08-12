using System.Collections.Generic;
using Dignite.Site.Pages;

namespace Dignite.Site.Admin.Schema;

/// <summary>One page of the site schema: a route, and the shapes of content that live under it.</summary>
public class SiteSchemaPageDto
{
    /// <summary>Tenant-unique. This is what a tool's <c>page</c> parameter takes.</summary>
    public string Name { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    /// <summary>
    /// The page's route template, e.g. <c>/about</c> for a page with nothing beneath it, or
    /// <c>/blog/{slug}</c> for one whose content is addressed by slug (总体设计 §3.2, §3.3).
    /// </summary>
    public string Route { get; set; } = default!;

    /// <summary>Whether this is the site's home page - derived from <see cref="Route"/>, not settable.</summary>
    public bool IsHomePage => PageRoute.IsHomeRoute(Route);

    public bool IsActive { get; set; }

    /// <summary>
    /// The parent page's name, or null for a top-level page - organizational only, unrelated to
    /// <see cref="Route"/>. Null, not a Guid: every other reference in this schema is by name too.
    /// </summary>
    public string? Parent { get; set; }

    /// <summary>The content types defined beneath this page (总体设计 §2.6).</summary>
    public List<SiteSchemaContentTypeDto> ContentTypes { get; set; } = new();
}
