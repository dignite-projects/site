using System.Collections.Generic;
using Dignite.Site.Public.Routing;

namespace Dignite.Site.Mcp.Routing;

/// <summary>
/// What <c>site_resolve_path</c> answers: which page, and which content under it, a path reaches. The public routing
/// service's answer carries more - the whole page, the whole content with its field values, the content type with its
/// fields, the site's languages again - and a client that asks where a path leads is not asking for a page's body.
/// <c>site_get_content</c> reads that.
/// </summary>
public class McpRouteMatch
{
    public bool Matched { get; set; }

    public RouteMatchKindDto Kind { get; set; }

    /// <summary>The language the path was resolved in: its prefix, else the default language.</summary>
    public string CultureName { get; set; } = default!;

    /// <summary>The page's name, as every other tool takes it.</summary>
    public string? Page { get; set; }

    /// <summary>The matched content's type, by name.</summary>
    public string? ContentType { get; set; }

    /// <summary>The matched content's slug - empty for the page's own content. Null when no content matched.</summary>
    public string? Slug { get; set; }

    /// <summary>The matched content's address, as a visitor reaches it.</summary>
    public string? Url { get; set; }

    /// <summary>The values the path named for the page's placeholders short of a slug; left out when there are none.</summary>
    public IDictionary<string, string>? FilterValues { get; set; }
}
