using System.Threading.Tasks;
using Dignite.Site.Public.Routing;
using Dignite.Site.Public.Templates;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// A page rendered by the tenant's Scriban templates (<see cref="ScribanPageRenderer"/>) - what
/// <c>SiteRenderController</c> returns instead of a Razor view when the page's template has a published
/// version. Rendered in full before anything is written, so a template that fails leaves the response
/// untouched for the host's error handling.
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

    public virtual async Task ExecuteResultAsync(ActionContext context)
    {
        var renderer = context.HttpContext.RequestServices.GetRequiredService<ScribanPageRenderer>();
        var html = await renderer.RenderAsync(TemplateName, RenderSet, ViewModel, context);

        var response = context.HttpContext.Response;
        response.ContentType = ContentType;
        await response.WriteAsync(html, context.HttpContext.RequestAborted);
    }
}
