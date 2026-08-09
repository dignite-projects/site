using System.Collections.Generic;

namespace Dignite.Site.Admin.Schema;

/// <summary>One shape of content: the field arrangement a write of this type is validated against.</summary>
public class SiteSchemaContentTypeDto
{
    /// <summary>Unique within its page. This is what a tool's <c>contentType</c> parameter takes.</summary>
    public string Name { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    /// <summary>
    /// What this type is for, written for an AI client to read (总体设计 §6.2.6). This is the text a
    /// model uses to decide that "post a news item" means <c>news-item</c> rather than <c>post-article</c>
    /// - the platform deliberately does not do that mapping itself, so a blank description here is what
    /// makes the difference between the tool surface working well and working badly.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>The arrangement, in this type's own order.</summary>
    public List<SiteSchemaFieldDto> Fields { get; set; } = new();
}
