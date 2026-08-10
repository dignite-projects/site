using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Fields;
using Dignite.Site.Public.ContentTypes;
using Dignite.Site.Public.Contents;
using Dignite.Site.Public.Fields;
using Dignite.Site.Public.Seo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.Site.Public.Routing;

/// <summary>
/// Renders whatever <see cref="IRoutingPublicAppService"/> resolves, via classic MVC <c>View()</c> -
/// Scriban is out of scope for this first version (总体设计 §10 leaves the rendering-engine question open;
/// v1 sidesteps it by only having one engine).
/// <para>
/// Deliberately Contracts-only: this whole project is meant to be portable to a different host someday,
/// so every dependency here is an interface from <c>Dignite.Site.Public.Application.Contracts</c> - never
/// a domain type, never a concrete Application-layer class. That rules out the "resolve the route once,
/// reuse it for head metadata too" shortcut a same-process renderer could otherwise take -
/// <see cref="IHeadMetadataPublicAppService"/> re-resolves internally, and that's an accepted cost of
/// staying portable, not an oversight.
/// </para>
/// <para>
/// An unconstrained catch-all, deliberately <c>Order = 1</c> so every default-Order route in this app
/// (<c>robots.txt</c>, <c>sitemap.xml</c>, <see cref="Seo.FeedController"/>'s own constrained catch-all,
/// HttpApi, Swagger, Account pages, ...) gets first refusal - ASP.NET Core's endpoint matcher tries every
/// Order-0 candidate before ever considering Order-1, so this only fires once nothing more specific has
/// already claimed the URL.
/// </para>
/// </summary>
public class SiteRenderController : AbpController
{
    public const string FallbackTemplateName = "Default";

    protected IRoutingPublicAppService RoutingAppService { get; }
    protected IContentPublicAppService ContentAppService { get; }
    protected IContentTypePublicAppService ContentTypeAppService { get; }
    protected IFieldPublicAppService FieldAppService { get; }
    protected IHeadMetadataPublicAppService HeadMetadataAppService { get; }
    protected ICompositeViewEngine ViewEngine { get; }

    public SiteRenderController(
        IRoutingPublicAppService routingAppService,
        IContentPublicAppService contentAppService,
        IContentTypePublicAppService contentTypeAppService,
        IFieldPublicAppService fieldAppService,
        IHeadMetadataPublicAppService headMetadataAppService,
        ICompositeViewEngine viewEngine)
    {
        RoutingAppService = routingAppService;
        ContentAppService = contentAppService;
        ContentTypeAppService = contentTypeAppService;
        FieldAppService = fieldAppService;
        HeadMetadataAppService = headMetadataAppService;
        ViewEngine = viewEngine;
    }

    [AcceptVerbs("GET", "HEAD", Route = "/{**path}", Order = 1)]
    public virtual async Task<IActionResult> RenderAsync()
    {
        // HttpContext.Request.Path.Value, not the {**path} route capture: canonical/decoded. Passed raw,
        // culture prefix and all - IRoutingPublicAppService strips it server-side.
        var rawPath = HttpContext.Request.Path.Value ?? "/";

        var match = await RoutingAppService.ResolveAsync(new ResolvePathInput { Path = rawPath });

        if (!match.Matched || match.Page == null)
        {
            // No redirect table exists yet in this codebase (总体设计 §5.7/§8.5 design intent, not built) -
            // straight to 404 for v1.
            return NotFound();
        }

        // Sequential, not concurrent: ContentTypeAppService/ContentAppService/HeadMetadataAppService all
        // ultimately share one EF Core DbContext instance per ambient UnitOfWork (one HTTP request) -
        // running two of their calls at once risks EF Core's "a second operation started on this context
        // before a previous operation completed".
        var headMetadata = await HeadMetadataAppService.ResolveAsync(new ResolveHeadMetadataInput { Path = rawPath });

        SiteRenderViewModel? viewModel = match.Kind switch
        {
            RouteMatchKindDto.Page => await BuildListViewModelAsync(match, headMetadata),
            RouteMatchKindDto.ContentOfPage or RouteMatchKindDto.Content
                => await BuildContentViewModelAsync(match, headMetadata),
            _ => null
        };

        if (viewModel == null)
        {
            return NotFound();
        }

        var templateName = ResolveTemplateName(match.Page.Template);
        return View(templateName, viewModel);
    }

    /// <summary>
    /// Falls back to <see cref="FallbackTemplateName"/> not only when <c>Page.Template</c> is blank, but
    /// also when it names a view that does not actually exist - <c>Template</c> predates this controller
    /// (总体设计 §7.3 "a front end that lets the back end name a view") and nothing has ever validated it
    /// points at a real MVC view, so a stale, misspelled or repurposed value must degrade gracefully
    /// rather than 500 every request to the page that carries it.
    /// </summary>
    protected virtual string ResolveTemplateName(string? template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return FallbackTemplateName;
        }

        var result = ViewEngine.FindView(ControllerContext, template, isMainPage: true);
        return result.Success ? template : FallbackTemplateName;
    }

    protected virtual async Task<SiteRenderViewModel?> BuildContentViewModelAsync(RouteMatchDto match, HeadMetadataDto? headMetadata)
    {
        if (match.Content == null || match.ContentType == null)
        {
            // RouteMatchDto.Content/.ContentType are genuinely nullable (e.g. the content's own content
            // type was deleted out from under it in a narrow admin-delete race) - treat exactly like the
            // list branch treats the same situation: nothing to render, not a crash.
            return null;
        }

        var fieldsById = await GetFieldsByIdAsync(match.ContentType.Fields.Select(f => f.FieldId));

        return new SiteRenderViewModel
        {
            Page = match.Page!,
            CultureName = match.CultureName,
            FilterValues = new Dictionary<string, string>(match.FilterValues, StringComparer.OrdinalIgnoreCase),
            HeadMetadata = headMetadata,
            Content = new ContentRenderViewModel
            {
                Content = match.Content,
                ContentType = match.ContentType,
                Fields = SiteRenderFieldMapper.Build(match.Content, match.ContentType, fieldsById, listFieldsOnly: false)
            }
        };
    }

    protected virtual async Task<SiteRenderViewModel> BuildListViewModelAsync(RouteMatchDto match, HeadMetadataDto? headMetadata)
    {
        var page = match.Page!;

        // Sequential - see the DbContext-sharing note on RenderAsync.
        var contentTypes = await ContentTypeAppService.GetListByPageAsync(page.Id);
        var contentTypesById = contentTypes.Items.ToDictionary(ct => ct.Id);

        var fieldsById = await GetFieldsByIdAsync(contentTypesById.Values.SelectMany(ct => ct.Fields).Select(f => f.FieldId));

        var contents = await ContentAppService.GetListAsync(new GetPublicContentListInput
        {
            PageId = page.Id,
            CultureName = match.CultureName,
            MaxResultCount = 100 // v1: no paging UI - see plan
        });

        var items = new List<ContentListItemViewModel>(contents.Items.Count);
        foreach (var content in contents.Items)
        {
            if (!contentTypesById.TryGetValue(content.ContentTypeId, out var contentType))
            {
                continue; // content type removed since this content was written - skip, don't break the list
            }

            items.Add(new ContentListItemViewModel
            {
                Content = content,
                ContentType = contentType,
                Url = content.Url, // computed server-side by ContentPublicAppService
                Fields = SiteRenderFieldMapper.Build(content, contentType, fieldsById, listFieldsOnly: true)
            });
        }

        return new SiteRenderViewModel
        {
            Page = page,
            CultureName = match.CultureName,
            FilterValues = new Dictionary<string, string>(match.FilterValues, StringComparer.OrdinalIgnoreCase),
            HeadMetadata = headMetadata,
            List = new ContentListRenderViewModel { Items = items, TotalCount = (int)contents.TotalCount }
        };
    }

    protected virtual async Task<IReadOnlyDictionary<Guid, FieldDto>> GetFieldsByIdAsync(IEnumerable<Guid> fieldIds)
    {
        var distinctIds = fieldIds.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return new Dictionary<Guid, FieldDto>();
        }

        var fields = await FieldAppService.GetListAsync(distinctIds);
        return fields.Items.ToDictionary(f => f.Id);
    }
}
