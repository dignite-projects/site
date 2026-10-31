using System;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Dignite.Site.Localization;
using Dignite.Site.Public.Preview;
using Dignite.Site.Public.Routing;
using Dignite.Site.Public.Templates;
using global::Scriban.Syntax;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Volo.Abp;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Volo.Abp.ExceptionHandling;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// A page rendered by the tenant's Scriban templates (<see cref="ScribanPageRenderer"/>) - what
/// <c>SiteRenderController</c> returns instead of a Razor view when the page's template has a published
/// version. Rendered in full before anything is written, so a template that fails leaves the response
/// untouched for the host's error handling.
/// <para>
/// In draft mode (<see cref="PreviewBanner"/> set) the banner goes right after <c>&lt;body&gt;</c>, and a template
/// that fails does not reach the host's error page: the visitor holding the preview cookie is the template's author,
/// and is answered with a 500 page that keeps the banner - and so the way out - and says what failed.
/// </para>
/// </summary>
public class ScribanPageResult : IActionResult
{
    public const string ContentType = "text/html; charset=utf-8";

    public ScribanPageResult(string templateName, SiteTemplateRenderSetDto renderSet, SiteRenderViewModel viewModel)
    {
        TemplateName = templateName;
        RenderSet = renderSet;
        ViewModel = viewModel;
    }

    public string TemplateName { get; }

    public SiteTemplateRenderSetDto RenderSet { get; }

    public SiteRenderViewModel ViewModel { get; }

    /// <summary>
    /// The draft-mode banner (<see cref="SiteTemplatePreviewState.GetBannerHtml"/>), or null on a page rendered as
    /// published - which then renders exactly as it always did.
    /// </summary>
    public string? PreviewBanner { get; set; }

    public virtual async Task ExecuteResultAsync(ActionContext context)
    {
        var renderer = context.HttpContext.RequestServices.GetRequiredService<ScribanPageRenderer>();
        var response = context.HttpContext.Response;

        if (PreviewBanner == null)
        {
            var published = await renderer.RenderAsync(TemplateName, RenderSet, ViewModel, context);

            response.ContentType = ContentType;
            await response.WriteAsync(published, context.HttpContext.RequestAborted);
            return;
        }

        string html;
        try
        {
            html = SiteTemplatePreviewBanner.Inject(
                await renderer.RenderAsync(TemplateName, RenderSet, ViewModel, context),
                PreviewBanner);
        }
        // The template's failures, as ScribanTemplatePreviewRenderer counts them, plus a render that never called
        // site_head (a business exception). A request the client aborted - which Scriban may hand on wrapped in a
        // ScriptRuntimeException - propagates as it is.
        catch (Exception exception) when (!context.HttpContext.RequestAborted.IsCancellationRequested
                                          && exception is ScriptRuntimeException or TimeoutException
                                              or InvalidOperationException or BusinessException)
        {
            response.StatusCode = StatusCodes.Status500InternalServerError;
            html = CreateErrorPage(context, exception);
        }

        response.ContentType = ContentType;
        await response.WriteAsync(html, context.HttpContext.RequestAborted);
    }

    /// <summary>The page a failed draft-mode render answers with: the banner, and what went wrong.</summary>
    protected virtual string CreateErrorPage(ActionContext context, Exception exception)
    {
        var services = context.HttpContext.RequestServices;
        var localizer = services.GetRequiredService<IStringLocalizer<SiteResource>>();
        var encoder = HtmlEncoder.Default;

        return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Preview</title></head><body>" +
               PreviewBanner +
               "<main style=\"padding:16px;font:14px/1.5 system-ui\"><p>" +
               encoder.Encode(localizer["TemplatePreviewRenderError"]) +
               "</p><pre style=\"white-space:pre-wrap\">" +
               encoder.Encode(GetErrorMessage(context, exception)) +
               "</pre></main></body></html>";
    }

    /// <summary>
    /// The exception's message - for a Scriban failure it names the template, line and column. A business exception
    /// usually has no message of its own, only a code and data (<c>Site:090005</c> and the template's name), so it is
    /// put into words the way ABP answers an API call with one.
    /// </summary>
    protected virtual string GetErrorMessage(ActionContext context, Exception exception)
    {
        if (exception is IHasErrorCode hasErrorCode)
        {
            var converter = context.HttpContext.RequestServices.GetService<IExceptionToErrorInfoConverter>();
            var message = converter?.Convert(exception).Message;
            if (!string.IsNullOrWhiteSpace(message))
            {
                return message;
            }

            if (!string.IsNullOrWhiteSpace(hasErrorCode.Code))
            {
                return hasErrorCode.Code;
            }
        }

        return exception.Message;
    }
}
