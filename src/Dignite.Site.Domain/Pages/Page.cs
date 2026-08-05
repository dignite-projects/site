using System;
using System.Collections.Generic;
using Dignite.Site.ContentTypes;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Pages;

/// <summary>
/// A route node - the platform's authoritative routing table, one row at a time (总体设计 §2.2, §3.1).
/// <para>
/// A page is <i>only</i> a route, the content types beneath it, and a rendering hint. It has no "kind":
/// single page, list and detail are not modelled, they are what a front end decides to render when a URL
/// resolves to a page (with no slug) or to a content beneath it (with one). It carries no field
/// definitions and no SEO metadata either - those belong to <see cref="ContentType"/> and to the
/// contents themselves.
/// </para>
/// <para>
/// This is Dignite.Cms's <c>Section</c>, minus <c>SectionType</c>: Cms distinguished Single / Channel /
/// Structure up front, which is precisely the "kind" decision §2.2 declines to make.
/// </para>
/// </summary>
public class Page : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    protected Page()
    {
        ContentTypes = new List<ContentType>();
    }

    public Page(
        Guid id,
        string name,
        string displayName,
        string route,
        string? contentPathPattern = null,
        string? template = null,
        bool isHomePage = false,
        int order = 0,
        bool isActive = true,
        Guid? tenantId = null)
        : base(id)
    {
        SetName(name);
        SetDisplayName(displayName);
        SetRoute(route);
        SetContentPathPattern(contentPathPattern);
        Template = template;
        IsHomePage = isHomePage;
        Order = order;
        IsActive = isActive;
        TenantId = tenantId;

        ContentTypes = new List<ContentType>();
    }

    /// <summary>Unique name within the tenant. A stable handle for MCP tools and templates to name a page by.</summary>
    public virtual string Name { get; protected set; } = default!;

    public virtual string DisplayName { get; protected set; } = default!;

    /// <summary>
    /// The base path this page occupies, normalized to a leading slash and no trailing one:
    /// <c>/</c>, <c>/about</c>, <c>/blog</c>. This is the page's reason to exist.
    /// </summary>
    public virtual string Route { get; protected set; } = default!;

    /// <summary>
    /// How the URLs of contents beneath this page are composed - see <see cref="ContentPathPattern"/>.
    /// Null means the default, <c>{slug}</c>.
    /// </summary>
    public virtual string? ContentPathPattern { get; protected set; }

    /// <summary>
    /// Optional rendering hint. Only a front end that lets the back end name a view uses it (总体设计 §7.3
    /// Tier 0); a Razor Pages or external front end resolves its own templates and leaves this null.
    /// </summary>
    public virtual string? Template { get; protected set; }

    /// <summary>
    /// Whether this is the site's home page. Also what <c>hreflang</c>'s <c>x-default</c> points at
    /// (总体设计 §5.5).
    /// </summary>
    public virtual bool IsHomePage { get; protected set; }

    /// <summary>Navigation ordering.</summary>
    public virtual int Order { get; protected set; }

    /// <summary>
    /// An inactive page is not routable and contributes nothing to the sitemap, but keeps its contents.
    /// </summary>
    public virtual bool IsActive { get; protected set; }

    public virtual Guid? TenantId { get; protected set; }

    /// <summary>
    /// The content types built beneath this page. A one-to-many ownership, not a shared pool: types do
    /// not cross pages, and deleting a page takes its types with it. Reuse happens one level down, at
    /// the field level (总体设计 §2.5).
    /// </summary>
    public virtual ICollection<ContentType> ContentTypes { get; protected set; }

    public virtual void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), PageConsts.MaxNameLength);
    }

    public virtual void SetDisplayName(string displayName)
    {
        DisplayName = Check.NotNullOrWhiteSpace(displayName, nameof(displayName), PageConsts.MaxDisplayNameLength);
    }

    /// <summary>
    /// Normalizes and assigns the route. Normalization is not cosmetic: the route is matched against
    /// request paths, so <c>blog</c>, <c>/blog</c> and <c>/blog/</c> reaching the table as three
    /// different strings would make "is this route taken?" answer no when it should answer yes.
    /// </summary>
    public virtual void SetRoute(string route)
    {
        Route = NormalizeRoute(route);
    }

    public virtual void SetContentPathPattern(string? contentPathPattern)
    {
        if (!string.IsNullOrWhiteSpace(contentPathPattern) && !Pages.ContentPathPattern.IsValid(contentPathPattern))
        {
            throw new InvalidContentPathPatternException(contentPathPattern);
        }

        ContentPathPattern = string.IsNullOrWhiteSpace(contentPathPattern)
            ? null
            : Pages.ContentPathPattern.Normalize(contentPathPattern);
    }

    public virtual void SetTemplate(string? template)
    {
        Template = string.IsNullOrWhiteSpace(template) ? null : template.Trim().RemovePreFix("/");
    }

    public virtual void SetIsHomePage(bool isHomePage)
    {
        IsHomePage = isHomePage;
    }

    public virtual void SetOrder(int order)
    {
        Order = order;
    }

    public virtual void SetIsActive(bool isActive)
    {
        IsActive = isActive;
    }

    /// <summary>
    /// The canonical stored form of a route: exactly one leading slash, no trailing slash, except for
    /// the site root which stays <c>/</c>.
    /// </summary>
    public static string NormalizeRoute(string route)
    {
        Check.NotNullOrWhiteSpace(route, nameof(route));

        var normalized = "/" + route.Trim().Trim('/');
        return normalized.Length > 1 ? normalized : "/";
    }

    /// <summary>
    /// The full path of one content beneath this page: the route, plus whatever
    /// <see cref="ContentPathPattern"/> composes from the content's publish time and slug. A content
    /// with an empty slug - the single content of a home or "about" page - is the page route itself.
    /// </summary>
    public virtual string BuildContentPath(DateTime publishTime, string? slug)
    {
        var relative = Pages.ContentPathPattern.Build(ContentPathPattern, publishTime, slug);

        if (relative.Length == 0)
        {
            return Route;
        }

        return Route == "/" ? "/" + relative : Route + "/" + relative;
    }
}
