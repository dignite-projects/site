using System;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Public.Fields;
using Dignite.Site.Public.Localization;
using Dignite.Site.Public.Routing;
using Dignite.Site.Public.Seo;
using Dignite.Site.Public.Templates;
using Dignite.Site.Seo;
using Dignite.Site.Templates;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Localization;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// Renders a page of the site with a saved template version in place of the published one - the
/// <see cref="ISiteTemplatePreviewRenderer"/> the administration side previews a version through, in an
/// application that also serves the site.
/// <para>
/// The page is resolved and its model built exactly as <see cref="SiteRenderController"/> does it - the same
/// Public application services, the same <see cref="SiteRenderViewModelBuilder"/> - and rendered by the same
/// <see cref="ScribanPageRenderer"/>, so a preview differs from the live page only in the one version it
/// substitutes. Previewing a page template keeps the published <c>layout</c> around it; previewing
/// <c>layout</c> wraps the page's published template, or an empty body when the page is still rendered by
/// Razor (a Razor view cannot be rendered into a Scriban layout).
/// </para>
/// <para>
/// Render failures - a template error (<c>ScriptRuntimeException</c>, naming template, line and column), the
/// time budget (<see cref="TimeoutException"/>), a render that never called <c>site_head</c> - propagate as
/// they are; the caller shows them.
/// </para>
/// <para>
/// Exposed explicitly: ABP's naming convention only exposes an interface whose name the class name ends with,
/// and this class's name does not end with <c>SiteTemplatePreviewRenderer</c>.
/// </para>
/// </summary>
[ExposeServices(typeof(ISiteTemplatePreviewRenderer), typeof(ScribanTemplatePreviewRenderer))]
public class ScribanTemplatePreviewRenderer : ISiteTemplatePreviewRenderer, ITransientDependency
{
    /// <summary>
    /// The id of the empty page template a <c>layout</c> preview wraps when the page has no published Scriban
    /// template. <see cref="ScribanTemplateCache"/> keys a parsed template by version id and name, so a fixed id
    /// with the same empty body every time maps every such render to one entry per page template name - always
    /// the same, empty tree - and can never collide with a real version, whose id is a fresh GUID.
    /// </summary>
    public static readonly Guid EmptyPageTemplateId = Guid.Parse("8d3e6f2a-5b0c-4e71-9a4d-0c1f7e2b5a60");

    protected IRoutingPublicAppService RoutingAppService { get; }
    protected IFieldPublicAppService FieldAppService { get; }
    protected IHeadMetadataPublicAppService HeadMetadataAppService { get; }
    protected ISiteTemplatePublicAppService TemplateAppService { get; }
    protected ScribanPageRenderer PageRenderer { get; }
    protected SiteLanguageContext LanguageContext { get; }
    protected IHttpContextAccessor HttpContextAccessor { get; }

    public ScribanTemplatePreviewRenderer(
        IRoutingPublicAppService routingAppService,
        IFieldPublicAppService fieldAppService,
        IHeadMetadataPublicAppService headMetadataAppService,
        ISiteTemplatePublicAppService templateAppService,
        ScribanPageRenderer pageRenderer,
        SiteLanguageContext languageContext,
        IHttpContextAccessor httpContextAccessor)
    {
        RoutingAppService = routingAppService;
        FieldAppService = fieldAppService;
        HeadMetadataAppService = headMetadataAppService;
        TemplateAppService = templateAppService;
        PageRenderer = pageRenderer;
        LanguageContext = languageContext;
        HttpContextAccessor = httpContextAccessor;
    }

    public virtual async Task<SiteTemplatePreviewDto> RenderAsync(
        SiteTemplatePreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(request, nameof(request));
        Check.NotNull(request.Version, nameof(request.Version));

        // The page renderer's functions (url, render_field) and its time budget work off the request, so a
        // preview needs one - which every caller has: the admin API, the admin UI, the MCP endpoint.
        var httpContext = HttpContextAccessor.HttpContext
                          ?? throw new AbpException(
                              "Previewing a template renders a page of the site, which needs an HTTP request to " +
                              "render it in; there is none on this call.");

        var path = NormalizePath(request.Path);

        var match = await RoutingAppService.ResolveAsync(new ResolvePathInput { Path = path });
        if (!match.Matched || match.Page == null)
        {
            throw PathNotFound(path);
        }

        // Sequential, for the same one-DbContext reason as SiteRenderController.
        var headMetadata = await HeadMetadataAppService.ResolveAsync(new ResolveHeadMetadataInput { Path = path });

        var viewModel = await SiteRenderViewModelBuilder.BuildAsync(match, headMetadata, FieldAppService)
                        ?? throw PathNotFound(path); // a content match with nothing to render - a 404 live, too

        var pageTemplateName = SiteRenderViewModelBuilder.ResolveTemplateName(match.Page.Template);
        var renderSet = await GetRenderSetAsync(request, pageTemplateName);
        var templateName = IsLayout(request) ? pageTemplateName : request.TemplateName;

        cancellationToken.ThrowIfCancellationRequested();

        // What SiteRenderController records for the page's language helpers, and what SiteLanguageResultFilter
        // sets around every view it renders - neither runs for an API call, which is what a preview is.
        if (match.EnabledCultureNames.Count > 0)
        {
            LanguageContext.SetLanguages(new SiteLanguages(match.DefaultCultureName, match.EnabledCultureNames));
        }

        var languages = await LanguageContext.GetLanguagesAsync();
        var previousHeadMetadata = LanguageContext.HeadMetadata;
        LanguageContext.HeadMetadata = headMetadata;

        string html;
        try
        {
            var actionContext = new ActionContext(httpContext, httpContext.GetRouteData(), new ActionDescriptor());

            // The match's culture, as SiteRenderController.CultureScopedResult sets it around a live render.
            using (SiteTemplateDefaultCulture.Use(languages.DefaultCultureName))
            using (CultureHelper.Use(match.CultureName))
            {
                html = await PageRenderer.RenderAsync(templateName, renderSet, viewModel, actionContext);
            }
        }
        finally
        {
            // The API request that asked for the preview is not the page; leave it as it was.
            LanguageContext.HeadMetadata = previousHeadMetadata;
        }

        return new SiteTemplatePreviewDto
        {
            Html = html,
            Path = path,
            PageName = match.Page.Name,
            CultureName = match.CultureName,
            PageTemplate = match.Page.Template,
            VersionNumber = request.Version.Number
        };
    }

    /// <summary>
    /// The page's published render set with <see cref="SiteTemplatePreviewRequest.Version"/> substituted - a new
    /// DTO, never the one the application service returned, which an in-process implementation may share.
    /// </summary>
    protected virtual async Task<SiteTemplateRenderSetDto> GetRenderSetAsync(SiteTemplatePreviewRequest request, string pageTemplateName)
    {
        var published = await TemplateAppService.GetRenderSetAsync(pageTemplateName);

        if (IsLayout(request))
        {
            return new SiteTemplateRenderSetDto
            {
                Template = published.Template ?? CreateEmptyPageTemplate(),
                Layout = request.Version
            };
        }

        return new SiteTemplateRenderSetDto
        {
            Template = request.Version,
            Layout = published.Layout
        };
    }

    /// <summary>See <see cref="EmptyPageTemplateId"/>.</summary>
    protected virtual SiteTemplateVersionDto CreateEmptyPageTemplate()
    {
        return new SiteTemplateVersionDto
        {
            Id = EmptyPageTemplateId,
            Number = 0,
            Body = string.Empty
        };
    }

    protected virtual bool IsLayout(SiteTemplatePreviewRequest request)
    {
        return string.Equals(request.TemplateName, SiteTemplateConsts.LayoutName, StringComparison.Ordinal);
    }

    /// <summary>Null or empty is the home page; a missing leading slash is added.</summary>
    protected virtual string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        path = path.Trim();
        return path.StartsWith('/') ? path : "/" + path;
    }

    protected virtual BusinessException PathNotFound(string path)
    {
        return new BusinessException(SiteErrorCodes.SiteTemplatePreviewPathNotFound).WithData("Path", path);
    }
}
