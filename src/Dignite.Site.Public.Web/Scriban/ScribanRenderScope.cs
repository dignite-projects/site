using Dignite.Site.Public.Routing;
using Dignite.Site.Public.Templates;
using Microsoft.AspNetCore.Mvc;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// What one page render's functions share: the page being rendered, the request, the templates it renders
/// with, and what the render has done so far.
/// </summary>
public class ScribanRenderScope
{
    public ScribanRenderScope(SiteRenderViewModel viewModel, ActionContext actionContext, SiteTemplateRenderSetDto renderSet)
    {
        ViewModel = viewModel;
        ActionContext = actionContext;
        RenderSet = renderSet;
    }

    public SiteRenderViewModel ViewModel { get; }

    public ActionContext ActionContext { get; }

    /// <summary>
    /// The published templates this render was started with - its partials, and the version numbers of its
    /// assets that <c>asset_url</c> puts in an address. Fixed for the whole render.
    /// </summary>
    public SiteTemplateRenderSetDto RenderSet { get; }

    /// <summary>
    /// Set by <c>site_head</c>. A render that never set it shipped a page without its SEO head tags, which
    /// <see cref="ScribanPageRenderer"/> refuses.
    /// </summary>
    public bool SiteHeadCalled { get; set; }
}
