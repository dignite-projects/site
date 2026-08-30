using System;
using System.Collections.Generic;
using Dignite.Site.ContentTypes;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Pages;

/// <summary>
/// A page, read-shaped. Shared between the Admin and Public application services - both read the same
/// routing-table row, only Admin can write it (总体设计 §2.2).
/// </summary>
public class PageDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string Route { get; set; } = default!;

    public string Template { get; set; } = default!;

    /// <summary>
    /// Whether this is the site's home page - derived from <see cref="Route"/>, not an independently
    /// settable flag. See <see cref="Page.IsHomeRoute"/> for why.
    /// </summary>
    public bool IsHomePage => PageRoute.IsHomeRoute(Route);

    /// <summary>
    /// The page this one is organized under in the Admin UI, or null for a top-level page. Purely
    /// organizational - see <see cref="Dignite.Site.Pages.Page.ParentId"/>.
    /// </summary>
    public Guid? ParentId { get; set; }

    public bool IsActive { get; set; }

    /// <summary>
    /// The content types built beneath this page. Populated by a single-page read (<c>GetAsync</c>,
    /// <c>GetByRouteAsync</c>, <c>FindByNameAsync</c>) so a caller rendering a page never has to follow up
    /// with a separate content-types-by-page call. Null - not empty - from a list result or a route
    /// resolution's own mapping: both leave <c>Page.ContentTypes</c>, the underlying EF navigation
    /// collection, un-<c>Include</c>d (<c>IPageRepository</c>'s <c>includeDetails</c> defaults to
    /// <see langword="false"/>), so mapping it unconditionally would silently misreport "never asked for
    /// this" as "genuinely has none". Null is how this type stays honest about that distinction, even
    /// though the one current consumer (<c>ContentListTagHelper</c>) chooses not to rely on it and
    /// re-fetches unconditionally instead.
    /// </summary>
    public List<ContentTypeDto>? ContentTypes { get; set; }
}
