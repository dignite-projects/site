using Asp.Versioning;
using Dignite.Site.Fields;
using Dignite.Site.Public.Fields;
using Dignite.Site.Public.Localization;
using Dignite.Site.Public.Preview;
using Dignite.Site.Public.Scriban;
using Dignite.Site.Public.Seo;
using Dignite.Site.Public.Templates;
using Dignite.Site.Seo;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.Site.Public.Routing;

/// <summary>
/// Renders whatever <see cref="IRoutingPublicAppService"/> resolves, with one of two engines, chosen per page by
/// <c>Page.Template</c>: when the tenant has published a Scriban template of that name
/// (<see cref="ISiteTemplatePublicAppService"/>), with it - wrapped in the tenant's published <c>layout</c>, if
/// any - through <see cref="ScribanPageResult"/>; otherwise, exactly as before, the Razor view of that name via
/// classic MVC <c>View()</c>. A tenant thus moves to its own templates page by page, and a site that has none
/// renders as it always did.
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
/// <para>
/// Draft mode: <see cref="SiteTemplatePreviewResourceFilter"/> exchanges a preview link's token for the preview
/// cookie, and <see cref="SiteTemplatePreviewState"/> puts the version that cookie names into the page's render set,
/// with a banner on a Scriban page and <c>noindex</c>/<c>no-store</c> on every page.
/// </para>
/// </summary>
[ControllerName(ControllerName)]
[ServiceFilter(typeof(SiteTemplatePreviewResourceFilter))]
public class SiteRenderController : AbpController
{
    public const string ControllerName = "SiteRender";

    protected IRoutingPublicAppService RoutingAppService { get; }
    protected IFieldPublicAppService FieldAppService { get; }
    protected IHeadMetadataPublicAppService HeadMetadataAppService { get; }
    protected ISiteTemplatePublicAppService TemplateAppService { get; }

    public SiteRenderController(
        IRoutingPublicAppService routingAppService,
        IFieldPublicAppService fieldAppService,
        IHeadMetadataPublicAppService headMetadataAppService,
        ISiteTemplatePublicAppService templateAppService)
    {
        RoutingAppService = routingAppService;
        FieldAppService = fieldAppService;
        HeadMetadataAppService = headMetadataAppService;
        TemplateAppService = templateAppService;
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

        // For the view's language helpers (GitHub issue #75): the site's languages straight from the match,
        // rather than a second read of the setting the match was resolved with, and the head metadata the
        // switcher finds this page's translations in. Only once the page is certain to render - a 404 must not
        // leave a re-executed error page offering translations of a page that was never shown.
        var languageContext = LazyServiceProvider.LazyGetRequiredService<SiteLanguageContext>();
        if (match.EnabledCultureNames.Count > 0)
        {
            languageContext.SetLanguages(new SiteLanguages(match.DefaultCultureName, match.EnabledCultureNames));
        }

        languageContext.HeadMetadata = headMetadata;

        // One required template for every RouteMatchKindDto - the template itself branches on whether
        // viewModel.Content is null (a list/index) or populated (总体设计 §7.3; see Default.cshtml). The
        // tenant's published Scriban template of that name wins; without one, a missing or misconfigured
        // Page.Template throws (standard ASP.NET Core view-not-found) rather than silently degrading - issue
        // #53. Looked up last, after the other reads, for the same one-DbContext reason as above.
        var templateName = ResolveTemplateName(match.Page.Template);
        var renderSet = await TemplateAppService.GetRenderSetAsync(templateName);

        // Draft mode: a visitor holding a valid preview cookie sees the version it names in its slot. Unchanged -
        // the very DTO - for everyone else. After the render-set read, for the same one-DbContext reason.
        var previewState = LazyServiceProvider.LazyGetRequiredService<SiteTemplatePreviewState>();
        renderSet = await previewState.ApplyAsync(templateName, renderSet);
        var previewing = previewState.Ticket != null;

        IActionResult result;
        if (renderSet.Template != null)
        {
            result = new ScribanPageResult(templateName, renderSet, viewModel)
            {
                PreviewBanner = previewing ? previewState.GetBannerHtml(ControllerContext) : null
            };
        }
        else
        {
            // A page still rendered by Razor gets the preview headers but no banner: the view's output is not
            // this controller's to rewrite.
            result = View(templateName, viewModel);
        }

        if (previewing)
        {
            // The page's own robots meta tag is the live page's; this header is what keeps the preview out of
            // indexes and caches.
            previewState.ApplyResponseHeaders(Response);
        }

        return new CultureScopedResult(result, match.CultureName);
    }

    /// <summary>
    /// Executes the wrapped result - a Razor <see cref="ViewResult"/> or a <see cref="ScribanPageResult"/> - with
    /// <see cref="CultureInfo.CurrentCulture"/>/
    /// <see cref="CultureInfo.CurrentUICulture"/> set to the resolved content culture, so the view's own
    /// culture-sensitive formatting (<c>ToString("d")</c> in a field template, <c>IStringLocalizer</c>)
    /// follows the URL's language rather than whatever <c>UseAbpRequestLocalization()</c> picked from the
    /// admin cookie / Accept-Language header. The site's default language, the last one
    /// <see cref="SiteTemplateResource"/>'s texts fall back to, is set around it by
    /// <see cref="SiteLanguageResultFilter"/>, as for every other view.
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
    public class CultureScopedResult : IActionResult
    {
        private readonly IActionResult _inner;
        private readonly string _cultureName;

        public CultureScopedResult(IActionResult inner, string cultureName)
        {
            _inner = inner;
            _cultureName = cultureName;
        }

        /// <summary>The result rendered inside the culture scope.</summary>
        public IActionResult Inner => _inner;

        public string CultureName => _cultureName;

        public async Task ExecuteResultAsync(ActionContext context)
        {
            var culture = CultureInfo.GetCultureInfo(_cultureName);

            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            await _inner.ExecuteResultAsync(context);
        }
    }

    /// <summary>See <see cref="SiteRenderViewModelBuilder.ResolveTemplateName"/>.</summary>
    protected virtual string ResolveTemplateName(string template)
    {
        return SiteRenderViewModelBuilder.ResolveTemplateName(template);
    }

    protected virtual SiteRenderViewModel BuildPageViewModel(RouteMatchDto match, HeadMetadataDto? headMetadata)
    {
        return SiteRenderViewModelBuilder.BuildPage(match, headMetadata);
    }

    /// <summary>Fetches the field definitions through <see cref="GetFieldsByIdAsync"/>, so an override of it still applies.</summary>
    protected virtual Task<SiteRenderViewModel?> BuildContentViewModelAsync(RouteMatchDto match, HeadMetadataDto? headMetadata)
    {
        return SiteRenderViewModelBuilder.BuildContentAsync(match, headMetadata, GetFieldsByIdAsync);
    }

    protected virtual Task<IReadOnlyDictionary<Guid, FieldDto>> GetFieldsByIdAsync(IEnumerable<Guid> fieldIds)
    {
        return SiteRenderViewModelBuilder.GetFieldsByIdAsync(fieldIds, FieldAppService);
    }
}
