using Dignite.Site.Public.Routing;
using Microsoft.AspNetCore.Mvc;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// What one page render's functions share: the page being rendered, the request, and what the render has
/// done so far.
/// </summary>
public class ScribanRenderScope
{
    public ScribanRenderScope(SiteRenderViewModel viewModel, ActionContext actionContext)
    {
        ViewModel = viewModel;
        ActionContext = actionContext;
    }

    public SiteRenderViewModel ViewModel { get; }

    public ActionContext ActionContext { get; }

    /// <summary>
    /// Set by <c>site_head</c>. A render that never set it shipped a page without its SEO head tags, which
    /// <see cref="ScribanPageRenderer"/> refuses.
    /// </summary>
    public bool SiteHeadCalled { get; set; }
}
