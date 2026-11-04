using System.Collections.Generic;

namespace Dignite.Site.Mcp.ContentTypes;

/// <summary>
/// A content type as <c>site_create_content_type</c> and <c>site_update_content_type</c> return it: addressed by names, like
/// everything a client sends, instead of the application service's DTO, whose fields are Guids of the field library that no
/// tool takes, and whose audit columns say nothing to a client.
/// </summary>
public class McpContentTypeSummary
{
    public string Page { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>The arrangement, in order.</summary>
    public List<McpContentTypeFieldSummary> Fields { get; set; } = new();
}

/// <summary>One field of <see cref="McpContentTypeSummary"/>: the field's name and how the type uses it.</summary>
public class McpContentTypeFieldSummary
{
    public string Field { get; set; } = default!;

    public bool Required { get; set; }

    public bool Searchable { get; set; }

    public bool ShowInList { get; set; }

    public string? DisplayName { get; set; }

    public int Order { get; set; }
}
