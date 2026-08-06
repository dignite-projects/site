using System.Collections.Generic;

namespace Dignite.Site.Admin.Schema;

/// <summary>One page of the site schema: a route, and the shapes of content that live under it.</summary>
public class SiteSchemaPageDto
{
    /// <summary>Tenant-unique. This is what a tool's <c>page</c> parameter takes.</summary>
    public string Name { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    /// <summary>The URL prefix this page owns, e.g. <c>/blog</c> (总体设计 §3.2).</summary>
    public string Route { get; set; } = default!;

    /// <summary>
    /// How a content's slug is arranged beneath the route, e.g. <c>{year}/{month}/{slug}</c>. Null means
    /// the slug follows the route directly (总体设计 §3.3).
    /// </summary>
    public string? ContentPathPattern { get; set; }

    public bool IsHomePage { get; set; }

    public bool IsActive { get; set; }

    /// <summary>The content types defined beneath this page (总体设计 §2.6).</summary>
    public List<SiteSchemaContentTypeDto> ContentTypes { get; set; } = new();
}
