using Dignite.Site.Themes;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Admin.Themes;

/// <summary>
/// The renderer the administration side previews and trial-renders with: <c>Dignite.Site.Public.Web</c>'s, when the
/// application has it; else the site's, over HTTP (<see cref="SiteThemeRemotePreviewRenderer"/>), when the application
/// that serves the site is configured; else none, and a preview fails with
/// <see cref="SiteErrorCodes.SiteTemplatePreviewUnavailable"/> and a trial render says it rendered nothing.
/// </summary>
public class SiteThemePreviewRendererProvider : ITransientDependency
{
    protected IAbpLazyServiceProvider LazyServiceProvider { get; }

    public SiteThemePreviewRendererProvider(IAbpLazyServiceProvider lazyServiceProvider)
    {
        LazyServiceProvider = lazyServiceProvider;
    }

    /// <summary>The renderer to use, or null when there is none.</summary>
    public virtual ISiteThemePreviewRenderer? Find()
    {
        var local = LazyServiceProvider.LazyGetService<ISiteThemePreviewRenderer>();
        if (local != null)
        {
            return local;
        }

        var remote = LazyServiceProvider.LazyGetRequiredService<SiteThemeRemotePreviewRenderer>();
        return remote.IsConfigured ? remote : null;
    }
}
