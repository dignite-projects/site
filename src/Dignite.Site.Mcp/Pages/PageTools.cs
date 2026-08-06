using System.ComponentModel;
using System.Threading.Tasks;
using Dignite.Site.Admin.Pages;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Mcp.Naming;
using Dignite.Site.Pages;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Mcp.Pages;

/// <summary>
/// The site-building surface: a page is a node of the routing table (总体设计 §2.2, §3.1).
/// <para>
/// There is no <c>list_pages</c> - <c>get_site_schema</c> already returns every page with its route and
/// the content types beneath it, and a second, thinner listing would only give a model a reason to skip
/// the schema.
/// </para>
/// </summary>
[McpServerToolType]
public class PageTools : ITransientDependency
{
    protected IPageAdminAppService PageAppService { get; }

    protected SiteMcpNameResolver NameResolver { get; }

    public PageTools(IPageAdminAppService pageAppService, SiteMcpNameResolver nameResolver)
    {
        PageAppService = pageAppService;
        NameResolver = nameResolver;
    }

    [McpServerTool(Name = "create_page", Title = "Create a page")]
    [Description(
        "Creates a page - a node of the site's routing table, owning a URL prefix. Content types are " +
        "defined beneath a page, and contents beneath those, so a page is the first thing to create when " +
        "building a new section.")]
    [Authorize(AdminPermissions.Pages.Create)]
    public virtual Task<PageDto> CreatePageAsync(
        [Description("Machine name, unique across the site, e.g. 'blog'. This is how every other tool addresses this page.")]
        string name,
        [Description("Human-readable name, e.g. 'Blog'.")]
        string displayName,
        [Description("The URL prefix this page owns, starting with a slash, e.g. '/blog'. Use '/' for the home page.")]
        string route,
        [Description(
            "How a content's URL is arranged under the route, e.g. '{publishTime:yyyy/MM}/{slug}' for " +
            "'<route>/2026/07/<slug>'. Omit for the usual '<route>/<slug>'. Must contain '{slug}'; the " +
            "only other placeholder is '{publishTime:FORMAT}', FORMAT being a .NET date format such as " +
            "'yyyy', 'yyyy/MM' or 'yyyy/MM/dd' - there is no separate '{year}', '{month}' or '{day}' token.")]
        string? contentPathPattern = null,
        [Description("Whether this page is the site's home page. At most one page can be.")]
        bool isHomePage = false,
        [Description("Sort order among pages. Lower comes first.")]
        int order = 0,
        [Description("Whether the page is live. An inactive page is not routed.")]
        bool isActive = true)
    {
        return PageAppService.CreateAsync(new CreatePageDto
        {
            Name = name,
            DisplayName = displayName,
            Route = route,
            ContentPathPattern = contentPathPattern,
            IsHomePage = isHomePage,
            Order = order,
            IsActive = isActive
        });
    }

    [McpServerTool(Name = "update_page", Title = "Update a page", Idempotent = true)]
    [Description(
        "Updates a page. Anything left null keeps its current value. Changing 'route' or " +
        "'contentPathPattern' changes the URL of every content beneath this page, so old links stop " +
        "working.")]
    [Authorize(AdminPermissions.Pages.Update)]
    public virtual async Task<PageDto> UpdatePageAsync(
        [Description("The page's current machine name, from get_site_schema.")]
        string page,
        [Description("A new machine name. Omit to keep the current one.")]
        string? name = null,
        [Description("New human-readable name. Omit to keep it.")]
        string? displayName = null,
        [Description("New URL prefix. Omit to keep it - see the warning above before changing it.")]
        string? route = null,
        [Description(
            "New content path pattern - see create_page for the placeholder syntax. Omit to keep the " +
            "current one; pass an empty string to clear it back to the default '{slug}'. See the warning " +
            "above: either way, this changes the URL of every content beneath this page.")]
        string? contentPathPattern = null,
        [Description("Whether this is the home page. Omit to keep it.")]
        bool? isHomePage = null,
        [Description("New sort order. Omit to keep it.")]
        int? order = null,
        [Description("Whether the page is live. Omit to keep it.")]
        bool? isActive = null)
    {
        var current = await NameResolver.GetPageAsync(page);

        return await PageAppService.UpdateAsync(current.Id, new UpdatePageDto
        {
            Name = name ?? current.Name,
            DisplayName = displayName ?? current.DisplayName,
            Route = route ?? current.Route,
            // Omitting the argument means "keep the current pattern": null ?? current.X evaluates to
            // current.X. An explicit empty string is different - "" is not null, so `??` leaves it alone
            // and it reaches Page.SetContentPathPattern, which treats a blank string the same as null and
            // clears the pattern back to the default '{slug}'. That is deliberate, not a gap: it mirrors
            // how the domain already treats blank and null as the same value, and the parameter
            // description above says so, so a model reaching for "" does not erase a pattern by accident.
            ContentPathPattern = contentPathPattern ?? current.ContentPathPattern,
            Template = current.Template,
            IsHomePage = isHomePage ?? current.IsHomePage,
            Order = order ?? current.Order,
            IsActive = isActive ?? current.IsActive
        });
    }

    [McpServerTool(Name = "delete_page", Title = "Delete a page", Destructive = true)]
    [Description(
        "Deletes a page AND EVERYTHING UNDER IT - every content type defined on it and every content of " +
        "those types, in every language. This is the only tool here that removes a whole section of the " +
        "site in one call, and it cannot be undone. If this page is the home page, the site is left with " +
        "none, which is what hreflang's x-default points at - set another page as home first if that " +
        "matters. Confirm with the user first.")]
    [Authorize(AdminPermissions.Pages.Delete)]
    public virtual async Task<string> DeletePageAsync(
        [Description("The page's machine name, from get_site_schema.")] string page)
    {
        var current = await NameResolver.GetPageAsync(page);

        // Deleted explicitly by PageManager, not by a database-level cascade: these entities are
        // soft-deleted (FullAuditedAggregateRoot), so a delete is an UPDATE and ON DELETE CASCADE never
        // fires (总体设计 §2.5; see PageManager.DeleteAsync). Nothing here counts what went with it,
        // because the count would be stale by the time it was reported - the warning above is the guard.
        await PageAppService.DeleteAsync(current.Id);

        return current.IsHomePage
            ? $"Deleted page '{page}' and all content types and contents beneath it. It was the site's " +
              "home page, so the site now has no home page."
            : $"Deleted page '{page}' and all content types and contents beneath it.";
    }
}
