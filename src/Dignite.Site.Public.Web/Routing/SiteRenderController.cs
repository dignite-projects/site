using Asp.Versioning;
using Dignite.Site.Fields;
using Dignite.Site.Public.Fields;
using Dignite.Site.Public.Seo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
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
[ControllerName(ControllerName)]
public class SiteRenderController : AbpController
{
    public const string ControllerName = "SiteRender";
    public const string FallbackTemplateName = "Default";

    protected IRoutingPublicAppService RoutingAppService { get; }
    protected IFieldPublicAppService FieldAppService { get; }
    protected IHeadMetadataPublicAppService HeadMetadataAppService { get; }
    protected ICompositeViewEngine ViewEngine { get; }

    public SiteRenderController(
        IRoutingPublicAppService routingAppService,
        IFieldPublicAppService fieldAppService,
        IHeadMetadataPublicAppService headMetadataAppService,
        ICompositeViewEngine viewEngine)
    {
        RoutingAppService = routingAppService;
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

        // Sequential, not concurrent: FieldAppService/HeadMetadataAppService both ultimately share one EF
        // Core DbContext instance per ambient UnitOfWork (one HTTP request) - running two of their calls at
        // once risks EF Core's "a second operation started on this context before a previous operation
        // completed".
        var headMetadata = await HeadMetadataAppService.ResolveAsync(new ResolveHeadMetadataInput { Path = rawPath });

        SiteRenderViewModel? viewModel = match.Kind switch
        {
            RouteMatchKindDto.Page => BuildPageViewModel(match, headMetadata),
            RouteMatchKindDto.ContentOfPage or RouteMatchKindDto.Content
                => await BuildContentViewModelAsync(match, headMetadata),
            _ => null
        };

        if (viewModel == null)
        {
            return NotFound();
        }

        // Page.Template and Page.ContentTemplate name two independent views, not two ends of one
        // branching template - RouteMatchKindDto.Page has no content fetched yet (a list/index), while
        // ContentOfPage/Content already carry one fully hydrated Content, so picking between them here
        // rather than inside a shared view keeps each view free of "which case am I in" checks.
        var templateName = ResolveTemplateName(
            match.Kind == RouteMatchKindDto.Page ? match.Page.Template : match.Page.ContentTemplate);
        return new CultureScopedViewResult(View(templateName, viewModel), match.CultureName);
    }

    /// <summary>
    /// Renders the wrapped <see cref="ViewResult"/> with <see cref="CultureInfo.CurrentCulture"/>/
    /// <see cref="CultureInfo.CurrentUICulture"/> set to the resolved content culture, so the view's own
    /// culture-sensitive formatting (<c>ToString("d")</c> in a field template, <c>IStringLocalizer</c>)
    /// follows the URL's language rather than whatever <c>UseAbpRequestLocalization()</c> picked from the
    /// admin cookie / Accept-Language header.
    /// <para>
    /// A result wrapper, not an assignment inside <c>RenderAsync</c>, because the latter provably does not
    /// work: <c>CurrentCulture</c> is AsyncLocal-backed, and an async method restores its caller's
    /// ExecutionContext when it completes - an assignment made in the action method is gone by the time
    /// MVC's invoker executes the returned result. Setting it here, inside <see cref="ExecuteResultAsync"/>
    /// immediately before awaiting the inner result, keeps the assignment in the same async flow that
    /// renders the view, which is the direction ExecutionContext changes do propagate. Scoped to this one
    /// result on purpose - the shared host's own localization pipeline (admin UI, Swagger, Account) stays
    /// untouched, and nothing is written back to the <c>.AspNetCore.Culture</c> cookie, which the admin
    /// side owns.
    /// </para>
    /// </summary>
    protected class CultureScopedViewResult : IActionResult
    {
        private readonly ViewResult _inner;
        private readonly string _cultureName;

        public CultureScopedViewResult(ViewResult inner, string cultureName)
        {
            _inner = inner;
            _cultureName = cultureName;
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            var culture = CultureInfo.GetCultureInfo(_cultureName);

            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            await _inner.ExecuteResultAsync(context);
        }
    }

    /// <summary>
    /// Falls back to <see cref="FallbackTemplateName"/> not only when the resolved template name is blank,
    /// but also when it names a view that does not actually exist - <c>Page.Template</c>/
    /// <c>Page.ContentTemplate</c> predate this controller (总体设计 §7.3 "a front end that lets the back
    /// end name a view") and nothing has ever validated either one points at a real MVC view, so a stale,
    /// misspelled or repurposed value must degrade gracefully rather than 500 every request to the page
    /// that carries it.
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

    protected virtual SiteRenderViewModel BuildPageViewModel(RouteMatchDto match, HeadMetadataDto? headMetadata)
    {
        var (fieldFilters, publishedAfter, publishedBefore) = SiteRenderFilterValueMapper.Build(match.FilterValues);
        return new SiteRenderViewModel
        {
            Page = match.Page!,
            CultureName = match.CultureName,
            FieldFilters = fieldFilters,
            PublishedAfter = publishedAfter,
            PublishedBefore = publishedBefore,
            HeadMetadata = headMetadata
        };
    }

    protected virtual async Task<SiteRenderViewModel?> BuildContentViewModelAsync(RouteMatchDto match, HeadMetadataDto? headMetadata)
    {
        if (match.Content == null || match.ContentType == null)
        {
            // RouteMatchDto.Content/.ContentType are genuinely nullable (e.g. the content's own content
            // type was deleted out from under it in a narrow admin-delete race) - nothing to render, not a
            // crash.
            return null;
        }

        var fieldsById = await GetFieldsByIdAsync(match.ContentType.Fields.Select(f => f.FieldId));
        var (fieldFilters, publishedAfter, publishedBefore) = SiteRenderFilterValueMapper.Build(match.FilterValues);

        return new SiteRenderViewModel
        {
            Page = match.Page!,
            CultureName = match.CultureName,
            FieldFilters = fieldFilters,
            PublishedAfter = publishedAfter,
            PublishedBefore = publishedBefore,
            HeadMetadata = headMetadata,
            Content = new ContentRenderViewModel
            {
                Content = match.Content,
                ContentType = match.ContentType,
                Fields = SiteRenderFieldMapper.Build(match.Content, match.ContentType, fieldsById, listFieldsOnly: false)
            }
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
