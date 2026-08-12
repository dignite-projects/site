using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Dignite.Site.ContentTypes;
using Dignite.Site.Contents;
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
        string? template = null,
        bool isActive = true,
        Guid? tenantId = null,
        Guid? parentId = null)
        : base(id)
    {
        SetName(name);
        SetDisplayName(displayName);
        SetRoute(route);
        Template = template;
        IsActive = isActive;
        TenantId = tenantId;
        ParentId = parentId;

        ContentTypes = new List<ContentType>();
    }

    /// <summary>Unique name within the tenant. A stable handle for MCP tools and templates to name a page by.</summary>
    public virtual string Name { get; protected set; } = default!;

    public virtual string DisplayName { get; protected set; } = default!;

    /// <summary>
    /// The page's route, normalized to a leading slash and no trailing one. A route template, not just a
    /// base path - see <see cref="PageRoute"/> for what that means and why. This is the page's reason to
    /// exist.
    /// </summary>
    public virtual string Route { get; protected set; } = default!;

    /// <summary>
    /// Optional rendering hint. Only a front end that lets the back end name a view uses it (总体设计 §7.3
    /// Tier 0); a Razor Pages or external front end resolves its own templates and leaves this null.
    /// </summary>
    public virtual string? Template { get; protected set; }

    /// <summary>
    /// The page this one is organized under in the Admin UI, or null for a top-level page. This is
    /// <b>purely organizational</b> - <see cref="Route"/> stays independent and free-form, and route
    /// resolution (<c>SiteRouteResolver</c>) never looks at this field. A child page can carry a route
    /// with no relation to its parent's; nothing here composes one from the other.
    /// </summary>
    public virtual Guid? ParentId { get; protected set; }

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
        var checkedName = Check.NotNullOrWhiteSpace(name, nameof(name), PageConsts.MaxNameLength);
        if (!IdentifierName.IsValid(checkedName))
        {
            throw new InvalidValueFormatException(nameof(Name), checkedName);
        }

        Name = checkedName;
    }

    public virtual void SetDisplayName(string displayName)
    {
        DisplayName = Check.NotNullOrWhiteSpace(displayName, nameof(displayName), PageConsts.MaxDisplayNameLength);
    }

    /// <summary>
    /// Normalizes, validates and assigns the route. Normalization is not cosmetic: the route is matched
    /// against request paths, so <c>blog</c>, <c>/blog</c> and <c>/blog/</c> reaching the table as three
    /// different strings would make "is this route taken?" answer no when it should answer yes.
    /// Validation catches an unrecognized <c>{...}</c> placeholder - see <see cref="PageRoute.IsValid"/>.
    /// </summary>
    public virtual void SetRoute(string route)
    {
        var normalized = NormalizeRoute(route);

        if (!Regex.IsMatch(normalized, PageConsts.RoutePattern))
        {
            throw new InvalidValueFormatException(nameof(Route), normalized);
        }

        if (!PageRoute.IsValid(normalized))
        {
            throw new InvalidPageRouteException(normalized);
        }

        Route = normalized;
    }

    public virtual void SetTemplate(string? template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            Template = null;
            return;
        }

        var trimmed = template.Trim().RemovePreFix("/");
        if (!Regex.IsMatch(trimmed, PageConsts.TemplatePattern))
        {
            throw new InvalidValueFormatException(nameof(Template), trimmed);
        }

        Template = trimmed;
    }

    /// <summary>
    /// Assigns the parent, or clears it with null. Existence and cycle checks are the caller's
    /// responsibility (<c>PageManager.CheckParentAsync</c>) - they need repository access this entity
    /// does not have, the same reason <see cref="Route"/> and <see cref="Name"/> uniqueness is checked
    /// there rather than here.
    /// </summary>
    public virtual void SetParent(Guid? parentId)
    {
        ParentId = parentId;
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
    /// The page's own address, independent of anything beneath it - see <see cref="PageRoute.GetPath"/>.
    /// What sitemap, canonical, hreflang and feed URLs are built from; never <see cref="Route"/> directly,
    /// since that may be a template carrying a placeholder.
    /// <para>
    /// A method, not a property: an expression-bodied property here is exactly the shape EF Core's
    /// convention-based discovery goes looking for, and this must never become a mapped column.
    /// </para>
    /// </summary>
    public virtual string GetPath()
    {
        return PageRoute.GetPath(Route);
    }

    /// <summary>
    /// Whether this page is the site's home page - see <see cref="PageRoute.IsHomeRoute"/>. A method, not
    /// a property, for the same reason <see cref="GetPath"/> is one: this must never become a mapped
    /// column.
    /// </summary>
    public virtual bool IsHomeRoute()
    {
        return PageRoute.IsHomeRoute(Route);
    }

    /// <summary>
    /// The full path of <paramref name="content"/> beneath this page. A content with an empty slug - the
    /// single content of a home or "about" page - is this page's own address; otherwise every placeholder
    /// in <see cref="Route"/> is filled in from <paramref name="content"/> - <c>{slug}</c> from
    /// <see cref="Contents.Content.Slug"/> itself, and any other placeholder from whatever field of that
    /// name the content has.
    /// </summary>
    public virtual string BuildContentPath(Content content)
    {
        return string.IsNullOrEmpty(content.Slug)
            ? GetPath()
            : PageRoute.Build(Route, (name, format) => ResolveFieldValue(content, name, format));
    }

    /// <summary>
    /// Resolves one route placeholder's rendered text from <paramref name="content"/>: a real property
    /// first (<c>Slug</c>, <c>PublishTime</c>, ...), by name and case-insensitively - the same lookup
    /// Dignite.Cms's <c>EntryDtoExtensions.GetPropertyValue</c> does - then, failing that,
    /// <see cref="Contents.Content.FlexFields"/>, since everything a content carries that is not one of
    /// its four system fields lives there (总体设计 §2.4). A name that resolves to neither is a
    /// configuration mistake in this page's route, not something to render around - it fails loudly, the
    /// same way Dignite.Cms's own version does, rather than emitting a URL with the placeholder text still
    /// in it.
    /// </summary>
    private static string ResolveFieldValue(Content content, string name, string? format)
    {
        var property = typeof(Content).GetProperty(
            name, BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.Instance);

        if (property != null)
        {
            return FormatFieldValue(property.GetValue(content), format);
        }

        if (content.FlexFields.TryGetValue(name, out var value))
        {
            return FormatFieldValue(value, format);
        }

        throw new AbpException(
            $"The route of page '{content.PageId}' refers to '{{{name}}}', but its content has no property or field of that name.");
    }

    /// <summary>
    /// Invariant culture, always: a route's placeholders are URL structure, not text shown to anybody, so
    /// they must not vary with the culture the request happens to run under. A Buddhist or Hijri calendar
    /// on the ambient culture would otherwise silently emit a different year into the URL.
    /// </summary>
    private static string FormatFieldValue(object? value, string? format)
    {
        if (format != null && value is IFormattable formattable)
        {
            return formattable.ToString(format, CultureInfo.InvariantCulture);
        }

        return value?.ToString() ?? string.Empty;
    }
}
