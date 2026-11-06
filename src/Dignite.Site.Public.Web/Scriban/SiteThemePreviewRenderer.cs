using System;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Public.Fields;
using Dignite.Site.Public.Localization;
using Dignite.Site.Public.Routing;
using Dignite.Site.Public.Seo;
using Dignite.Site.Public.Themes;
using Dignite.Site.Seo;
using Dignite.Site.Themes;
using global::Scriban.Syntax;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Localization;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// Renders a page of the site with a theme snapshot - the working copy or a release - in place of the live release:
/// the <see cref="ISiteThemePreviewRenderer"/> the administration side previews through, in an application that also
/// serves the site.
/// <para>
/// The page is resolved and its model built exactly as <see cref="SiteRenderController"/> does it - the same Public
/// application services, the same <see cref="SiteRenderViewModelBuilder"/> - and rendered by the same
/// <see cref="ScribanPageRenderer"/>, so a preview differs from the live page only in the theme it renders with. A page
/// whose template is not in the snapshot is rendered by a Razor view live, which a preview cannot render, and fails with
/// <see cref="SiteErrorCodes.SiteThemePreviewPageNotScriban"/>.
/// </para>
/// <para>
/// A template that fails at run time (<see cref="ScriptRuntimeException"/>, whose message names template, line and
/// column), runs out of time (<see cref="TimeoutException"/>), does not parse, names a layout the theme does not have
/// or writes past the output limit (<see cref="ScribanRenderException"/>; a working copy may be saved with errors)
/// fails the preview with <see cref="SiteErrorCodes.SiteTemplatePreviewRenderFailed"/>, carrying that message: as a
/// business exception it reaches every caller intact - the admin UI over HTTP, a client proxy, an MCP client - where
/// ABP would replace the original exception with "an internal error occurred". A render that never called
/// <c>site_head</c> already is a business exception and propagates as it is, and so does a request the client aborted
/// or a call the caller cancelled - and so does any other failure a service met along the way, which is the
/// application's, not the theme's.
/// </para>
/// <para>
/// The page is rendered as the site path, not as the administration request it is rendered from:
/// <see cref="SiteLanguageContext.RenderedPath"/> is set for the render, so <c>url</c> and the language switcher see
/// the page's language.
/// </para>
/// <para>
/// Exposed explicitly: ABP's naming convention only exposes an interface whose name the class name ends with.
/// </para>
/// </summary>
[ExposeServices(typeof(ISiteThemePreviewRenderer), typeof(SiteThemePreviewRenderer))]
public class SiteThemePreviewRenderer : ISiteThemePreviewRenderer, ITransientDependency
{
    protected IRoutingPublicAppService RoutingAppService { get; }
    protected IFieldPublicAppService FieldAppService { get; }
    protected IHeadMetadataPublicAppService HeadMetadataAppService { get; }
    protected ScribanPageRenderer PageRenderer { get; }
    protected SiteLanguageContext LanguageContext { get; }
    protected IHttpContextAccessor HttpContextAccessor { get; }
    protected IOptions<SiteScribanOptions> Options { get; }

    public SiteThemePreviewRenderer(
        IRoutingPublicAppService routingAppService,
        IFieldPublicAppService fieldAppService,
        IHeadMetadataPublicAppService headMetadataAppService,
        ScribanPageRenderer pageRenderer,
        SiteLanguageContext languageContext,
        IHttpContextAccessor httpContextAccessor,
        IOptions<SiteScribanOptions> options)
    {
        RoutingAppService = routingAppService;
        FieldAppService = fieldAppService;
        HeadMetadataAppService = headMetadataAppService;
        PageRenderer = pageRenderer;
        LanguageContext = languageContext;
        HttpContextAccessor = httpContextAccessor;
        Options = options;
    }

    public virtual async Task<SiteThemePreviewDto> RenderAsync(
        SiteThemePreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        Check.NotNull(request, nameof(request));
        Check.NotNull(request.Snapshot, nameof(request.Snapshot));

        // The page renderer's functions (url, render_field) and its time budget work off the request, so a
        // preview needs one - which every caller has: the admin API, the admin UI, the MCP endpoint.
        var httpContext = HttpContextAccessor.HttpContext
                          ?? throw new AbpException(
                              "Previewing a theme renders a page of the site, which needs an HTTP request to " +
                              "render it in; there is none on this call.");

        // The path may carry a query string, for the page of a paged list (/ja/blog?page=2): the path is what is
        // resolved and rendered as, the query string what the page number is read from, as a visitor's request's is.
        var (path, queryString) = SplitQueryString(request.Path);
        path = NormalizePath(path);

        var match = await RoutingAppService.ResolveAsync(new ResolvePathInput { Path = path });
        if (!match.Matched || match.Page == null)
        {
            throw PathNotFound(path);
        }

        // Sequential, for the same one-DbContext reason as SiteRenderController.
        var headMetadata = await HeadMetadataAppService.ResolveAsync(new ResolveHeadMetadataInput { Path = path });

        var currentPage = SiteRenderPageNumber.Parse(queryString, Options.Value.PageQueryKey);
        var viewModel = await SiteRenderViewModelBuilder.BuildAsync(match, headMetadata, FieldAppService, currentPage)
                        ?? throw PathNotFound(path); // a content match with nothing to render - a 404 live, too
        viewModel.Query = SiteRenderQuery.Parse(queryString);

        var renderSet = new SiteThemeRenderSet(request.Snapshot);
        var templatePath = SiteThemePaths.Normalize(SiteRenderViewModelBuilder.ResolveTemplateName(match.Page.Template));
        if (renderSet.FindTemplate(templatePath) == null)
        {
            throw new BusinessException(SiteErrorCodes.SiteThemePreviewPageNotScriban)
                .WithData("Template", templatePath)
                .WithData("Path", path);
        }

        cancellationToken.ThrowIfCancellationRequested();

        // What SiteRenderController records for the page's language helpers, and what SiteLanguageResultFilter
        // sets around every view it renders - neither runs for an API call, which is what a preview is.
        if (match.EnabledCultureNames.Count > 0)
        {
            LanguageContext.SetLanguages(new SiteLanguages(match.DefaultCultureName, match.EnabledCultureNames));
        }

        var languages = await LanguageContext.GetLanguagesAsync();
        var previousHeadMetadata = LanguageContext.HeadMetadata;
        var previousRenderedPath = LanguageContext.RenderedPath;
        LanguageContext.HeadMetadata = headMetadata;
        LanguageContext.RenderedPath = path;

        ScribanPageRender render;
        try
        {
            var actionContext = new ActionContext(httpContext, httpContext.GetRouteData(), new ActionDescriptor());

            // The match's culture, as SiteRenderController.CultureScopedResult sets it around a live render.
            using (SiteTemplateDefaultCulture.Use(languages.DefaultCultureName))
            using (CultureHelper.Use(match.CultureName))
            {
                render = await PageRenderer.RenderAsync(templatePath, renderSet, viewModel, actionContext, cancellationToken);
            }
        }
        // ScriptAbortException is a ScriptRuntimeException too, and a cancellation Scriban met in an awaited call
        // may come wrapped in one: when the client went away, or the caller cancelled, it is not the template's
        // failure. A ScribanRenderException is what BoundedScriptOutput raises past MaxOutputLength, what the cache
        // raises for a template that does not parse and what the renderer raises for a layout that is not there - all
        // the theme's failure too. Any other exception is a service's, and propagates as it is.
        catch (Exception exception) when (exception is TimeoutException or ScribanRenderException
                                          || (exception is ScriptRuntimeException
                                              && !httpContext.RequestAborted.IsCancellationRequested
                                              && !cancellationToken.IsCancellationRequested))
        {
            var failure = new BusinessException(SiteErrorCodes.SiteTemplatePreviewRenderFailed, innerException: exception)
                .WithData("Name", templatePath)
                .WithData("Message", exception.Message);

            // Where in the theme it happened, as the places themselves and not a message to be read for them.
            if ((exception as ScriptRuntimeException ?? exception.InnerException as ScriptRuntimeException)?.Span is { FileName: { Length: > 0 } file } span)
            {
                failure
                    .WithData("File", file)
                    .WithData("Line", span.Start.Line + 1)
                    .WithData("Column", span.Start.Column + 1);
            }

            throw failure;
        }
        finally
        {
            // The API request that asked for the preview is not the page; leave it as it was.
            LanguageContext.HeadMetadata = previousHeadMetadata;
            LanguageContext.RenderedPath = previousRenderedPath;
        }

        return new SiteThemePreviewDto
        {
            Html = render.Html,
            Path = path,
            PageName = match.Page.Name,
            CultureName = match.CultureName,
            PageTemplate = render.TemplatePath ?? templatePath,
            Layout = render.LayoutPath
        };
    }

    /// <summary>The path before the first <c>?</c>, and the query string from it on (null when there is none).</summary>
    protected virtual (string? Path, string? QueryString) SplitQueryString(string? path)
    {
        var queryIndex = path?.IndexOf('?') ?? -1;
        return queryIndex < 0 ? (path, null) : (path![..queryIndex], path[queryIndex..]);
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
