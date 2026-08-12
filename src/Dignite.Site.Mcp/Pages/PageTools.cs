using System;
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
        "building a new section. Comes with one content type already, named the same as the page and " +
        "carrying only the SEO field - call update_content_type on it to set the real field arrangement " +
        "(or create_content_type for an additional type, if this page needs more than one shape of " +
        "content).")]
    [Authorize(AdminPermissions.Pages.Create)]
    public virtual async Task<PageDto> CreatePageAsync(
        [Description("Machine name, unique across the site, e.g. 'blog'. This is how every other tool addresses this page.")]
        string name,
        [Description("Human-readable name, e.g. 'Blog'.")]
        string displayName,
        [Description(
            "The page's route template, starting with a slash. No placeholder means no content beneath " +
            "it, e.g. '/about' (use '/' for the home page - more generally, a route with nothing before " +
            "its first placeholder, e.g. '{slug?}' on its own, is the home page too). Embed '{slug}' " +
            "where the slug goes to have content beneath it and require every content there to have " +
            "one, e.g. '/blog/{slug}'; use '{slug?}' instead to also allow one content with an empty " +
            "slug, served at this page's own address, e.g. '/about/{slug?}'. Any other field the " +
            "content has can appear too - a system field or a custom one, it makes no difference - " +
            "optionally with a ':FORMAT' suffix, e.g. '{publishTime:yyyy-MM}' for '2026-07', e.g. " +
            "'/news/{publishTime:yyyy-MM}/{slug}' for '/news/2026-07/<slug>'. A FORMAT may only contain " +
            "letters, digits, '.', '_', '-' - never '/', which would be indistinguishable from the " +
            "slash between path segments.")]
        string route,
        [Description(
            "The parent page's machine name, for organizing this page under it in the Admin UI's tree. " +
            "Purely organizational - it has no effect on 'route' or on how requests are resolved against " +
            "it. Omit for a top-level page. Ignored when 'route' makes this the home page: the home page " +
            "is always the tree's root.")]
        string? parent = null,
        [Description("Whether the page is live. An inactive page is not routed.")]
        bool isActive = true)
    {
        var parentId = parent != null ? (Guid?)(await NameResolver.GetPageAsync(parent)).Id : null;

        return await PageAppService.CreateAsync(new CreatePageDto
        {
            Name = name,
            DisplayName = displayName,
            Route = route,
            ParentId = parentId,
            IsActive = isActive
        });
    }

    [McpServerTool(Name = "update_page", Title = "Update a page", Idempotent = true)]
    [Description(
        "Updates a page. Anything left null keeps its current value. Changing 'route' changes the URL " +
        "of every content beneath this page, so old links stop working.")]
    [Authorize(AdminPermissions.Pages.Update)]
    public virtual async Task<PageDto> UpdatePageAsync(
        [Description("The page's current machine name, from get_site_schema.")]
        string page,
        [Description("A new machine name. Omit to keep the current one.")]
        string? name = null,
        [Description("New human-readable name. Omit to keep it.")]
        string? displayName = null,
        [Description(
            "New route template - see create_page for the placeholder syntax. Omit to keep the current " +
            "one; see the warning above before changing it. Dropping '{slug}'/'{slug?}' turns a page that " +
            "has content beneath it into one that does not, and vice versa; switching between '{slug}' " +
            "and '{slug?}' changes whether an empty slug is allowed there. Changing this to or from a " +
            "route with nothing before its first placeholder also changes whether this page is the home " +
            "page - see create_page's note on 'route'. None of this re-validates contents that already " +
            "exist - only the next write to one of them sees the new rule.")]
        string? route = null,
        [Description(
            "New parent page's machine name, for reorganizing this page in the Admin UI's tree. Purely " +
            "organizational - has no effect on 'route'. Omit to keep the current parent; pass an empty " +
            "string to make this a top-level page. Ignored if this page's route is, or is becoming, the " +
            "home route: the home page is always the tree's root.")]
        string? parent = null,
        [Description("Whether the page is live. Omit to keep it.")]
        bool? isActive = null)
    {
        var current = await NameResolver.GetPageAsync(page);

        var parentId = current.ParentId;
        if (parent == string.Empty)
        {
            parentId = null;
        }
        else if (parent != null)
        {
            parentId = (await NameResolver.GetPageAsync(parent)).Id;
        }

        return await PageAppService.UpdateAsync(current.Id, new UpdatePageDto
        {
            Name = name ?? current.Name,
            DisplayName = displayName ?? current.DisplayName,
            Route = route ?? current.Route,
            Template = current.Template,
            ParentId = parentId,
            IsActive = isActive ?? current.IsActive
        });
    }

    [McpServerTool(Name = "delete_page", Title = "Delete a page", Destructive = true)]
    [Description(
        "Deletes a page AND EVERYTHING UNDER IT - every content type defined on it and every content of " +
        "those types, in every language. This is the only tool here that removes a whole section of the " +
        "site in one call, and it cannot be undone. Fails if the page has child pages in the Admin UI's " +
        "tree - move them elsewhere or delete them first with update_page/delete_page. If this page is " +
        "the home page, the site is left with none, which is what hreflang's x-default points at - set " +
        "another page as home first if that matters. Confirm with the user first.")]
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
