using System;
using System.Collections.Generic;
using Dignite.Site.Templates;

namespace Dignite.Site.Public.Templates;

/// <summary>
/// Everything the renderer needs to turn one page view into HTML, read in one query: the published template of
/// the requested name, the published layout around it, every published partial it may <c>include</c>, and the
/// published version id of every asset it may point at with <c>asset_url</c>.
/// </summary>
public class SiteTemplateRenderSetDto
{
    /// <summary>
    /// The published version of the requested template, or null when it is missing, unpublished, or not a
    /// page template at all (a name under <c>partials/</c> or <c>assets/</c> never renders a page).
    /// </summary>
    public SiteTemplateVersionDto? Template { get; set; }

    /// <summary>The published version of the template named <c>layout</c>, or null when there is none.</summary>
    public SiteTemplateVersionDto? Layout { get; set; }

    /// <summary>
    /// The published version of every template under <c>partials/</c>, keyed by full name
    /// (<c>partials/post-card</c>). Read with the page's template rather than on each <c>include</c>: one query
    /// per page view instead of one per include, and the set a render sees is fixed when it starts.
    /// </summary>
    public Dictionary<string, SiteTemplateVersionDto> Partials { get; set; } = new();

    /// <summary>
    /// The id of the published version of every template under <c>assets/</c>, keyed by full name
    /// (<c>assets/site.css</c>) - what <c>asset_url</c> puts in the address (<c>?v=</c> and the id as 32 hex
    /// digits), so a publish changes the URL. The id and not the version number: numbers restart at 1 when a
    /// template is deleted and created again under the same name, and an address that browsers and CDNs may keep
    /// for a year must never be reused for other bytes; an id is unique for good. Bodies are not read here;
    /// <c>GET /site-assets/</c> reads them.
    /// </summary>
    public Dictionary<string, Guid> Assets { get; set; } = new();
}
