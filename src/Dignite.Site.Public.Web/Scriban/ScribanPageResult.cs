using System;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Dignite.Site.Localization;
using Dignite.Site.Public.Preview;
using Dignite.Site.Public.Routing;
using Dignite.Site.Public.Themes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Scriban.Syntax;
using Volo.Abp;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Volo.Abp.Localization;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// A page rendered by the tenant's Scriban templates (<see cref="ScribanPageRenderer"/>) - what
/// <c>SiteRenderController</c> returns instead of a Razor view when the theme it renders with has a template at the
/// page's <c>Template</c>. Rendered in full before anything is written, so a template that fails leaves the response
/// untouched for the host's error handling - except in a preview (<see cref="ShowsErrors"/>), where the author is told
/// what failed.
/// </summary>
public class ScribanPageResult : IActionResult
{
    public const string ContentType = "text/html; charset=utf-8";

    public ScribanPageResult(string templatePath, SiteThemeRenderSet renderSet, SiteRenderViewModel viewModel)
    {
        TemplatePath = templatePath;
        RenderSet = renderSet;
        ViewModel = viewModel;
    }

    public string TemplatePath { get; }

    public SiteThemeRenderSet RenderSet { get; }

    public SiteRenderViewModel ViewModel { get; }

    /// <summary>The preview banner (<see cref="SitePreviewBanner"/>) to put at the top of the page, or null.</summary>
    public string? Banner { get; init; }

    /// <summary>
    /// Whether a template that fails - at run time, past its time or output limit, without <c>site_head</c>, or a layout
    /// or include that is not there - is answered with a page that says so, status 500, instead of an exception. Set in
    /// a preview only: the visitor is then the theme's author, and a live page never shows a template's error.
    /// </summary>
    public bool ShowsErrors { get; init; }

    public virtual async Task ExecuteResultAsync(ActionContext context)
    {
        var renderer = context.HttpContext.RequestServices.GetRequiredService<ScribanPageRenderer>();

        ScribanPageRender render;
        try
        {
            render = await renderer.RenderAsync(TemplatePath, RenderSet, ViewModel, context);
        }
        catch (Exception exception) when (ShowsErrors && IsThemeFailure(exception, context.HttpContext))
        {
            await WriteErrorPageAsync(context, exception);
            return;
        }

        var html = Banner == null ? render.Html : SitePreviewBanner.Inject(render.Html, Banner);

        var response = context.HttpContext.Response;
        response.ContentType = ContentType;
        await response.WriteAsync(html, context.HttpContext.RequestAborted);
    }

    /// <summary>
    /// The failures that are the theme's: what <see cref="SiteThemePreviewRenderer"/> reports as such, and the
    /// <see cref="BusinessException"/> for a page that never reached <c>site_head</c>. A cancellation because the
    /// visitor went away is not one, and neither is any other exception a service threw along the way: that is the
    /// application's failure, left to the host's error handling.
    /// </summary>
    protected virtual bool IsThemeFailure(Exception exception, HttpContext httpContext)
    {
        return exception is TimeoutException or ScribanRenderException
               || exception is BusinessException { Code: SiteErrorCodes.SiteTemplateDidNotRenderSiteHead }
               || (exception is ScriptRuntimeException && !httpContext.RequestAborted.IsCancellationRequested);
    }

    /// <summary>
    /// A plain page with the banner, a title and the failure's message - a business error localized the way the API
    /// localizes it, any other the exception's own message, which names the template, line and column. In the request's
    /// UI culture, like the banner: around this result runs the page's content culture.
    /// </summary>
    protected virtual async Task WriteErrorPageAsync(ActionContext context, Exception exception)
    {
        var services = context.HttpContext.RequestServices;
        var uiCulture = context.HttpContext.Features.Get<IRequestCultureFeature>()?.RequestCulture.UICulture;

        string title;
        string message;
        using (uiCulture == null ? null : CultureHelper.Use(uiCulture))
        {
            title = services.GetRequiredService<IStringLocalizer<SiteResource>>()["PreviewRenderFailed", TemplatePath];
            message = exception is BusinessException
                ? services.GetRequiredService<IExceptionToErrorInfoConverter>().Convert(exception).Message ?? exception.Message
                : exception.Message;
        }

        var encoder = HtmlEncoder.Default;
        var html = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta name=\"robots\" content=\"noindex\"><title>" +
                   encoder.Encode(title) + "</title></head><body style=\"margin:0\">" + (Banner ?? string.Empty) +
                   "<main style=\"max-width:960px;margin:32px auto;padding:0 16px;font:15px/1.5 system-ui,sans-serif\"><h1 " +
                   "style=\"font-size:20px\">" + encoder.Encode(title) + "</h1><pre style=\"white-space:pre-wrap;" +
                   "background:#f3f4f6;padding:12px;border-radius:6px\">" + encoder.Encode(message) + "</pre></main></body></html>";

        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status500InternalServerError;
        response.ContentType = ContentType;
        await response.WriteAsync(html, context.HttpContext.RequestAborted);
    }
}
