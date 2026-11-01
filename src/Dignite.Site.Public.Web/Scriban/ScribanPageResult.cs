using System.Threading.Tasks;
using Dignite.Site.Public.Routing;
using Dignite.Site.Public.Themes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// A page rendered by the tenant's Scriban templates (<see cref="ScribanPageRenderer"/>) - what
/// <c>SiteRenderController</c> returns instead of a Razor view when the live release has a template at the page's
/// <c>Template</c>. Rendered in full before anything is written, so a template that fails leaves the response untouched
/// for the host's error handling.
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

    public virtual async Task ExecuteResultAsync(ActionContext context)
    {
        var renderer = context.HttpContext.RequestServices.GetRequiredService<ScribanPageRenderer>();
        var render = await renderer.RenderAsync(TemplatePath, RenderSet, ViewModel, context);

        var response = context.HttpContext.Response;
        response.ContentType = ContentType;
        await response.WriteAsync(render.Html, context.HttpContext.RequestAborted);
    }
}
