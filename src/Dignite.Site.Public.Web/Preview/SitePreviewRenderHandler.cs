using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Themes;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.Public.Preview;

/// <summary>
/// Renders a page for an administration API deployed apart from the site - what <c>POST /site-preview/render</c>
/// (<see cref="SiteThemePreviewConsts.RenderPath"/>) answers. The administration API has no renderer, so it sends the
/// snapshot here, and the page is rendered by the site's own <see cref="ISiteThemePreviewRenderer"/>, with the site's
/// services, views and options: what a preview differs from the live page in is the theme, and nothing else.
/// <para>
/// Only a call that carries a valid <see cref="SitePreviewServiceTokenProtector"/> token is answered, which only an
/// application holding the shared Data Protection keys can write. The page is rendered for the tenant the token names,
/// not for one the request resolves: the call comes from a server, not from a visitor of that tenant's address.
/// </para>
/// <para>
/// A business exception of the renderer - a path no page answers, a template that fails - is part of the answer
/// (<see cref="SiteThemePreviewRenderResult.Error"/>), so the administration API raises it again as it is. Anything else
/// propagates and fails the call: it is the site's failure, not the theme's.
/// </para>
/// </summary>
public class SitePreviewRenderHandler : ITransientDependency
{
    protected SitePreviewServiceTokenProtector Protector { get; }
    protected ISiteThemePreviewRenderer Renderer { get; }
    protected ICurrentTenant CurrentTenant { get; }

    public SitePreviewRenderHandler(
        SitePreviewServiceTokenProtector protector,
        ISiteThemePreviewRenderer renderer,
        ICurrentTenant currentTenant)
    {
        Protector = protector;
        Renderer = renderer;
        CurrentTenant = currentTenant;
    }

    /// <summary>The page or the renderer's failure; null when the call has no valid token or nothing to render.</summary>
    public virtual async Task<SiteThemePreviewRenderResult?> HandleAsync(
        SiteThemePreviewRenderInput? input,
        CancellationToken cancellationToken = default)
    {
        if (input?.Snapshot == null || !Protector.TryUnprotect(input.Token, out var ticket))
        {
            return null;
        }

        using (CurrentTenant.Change(ticket.TenantId))
        {
            try
            {
                var preview = await Renderer.RenderAsync(
                    new SiteThemePreviewRequest { Path = input.Path, Snapshot = input.Snapshot },
                    cancellationToken);

                return new SiteThemePreviewRenderResult { Preview = preview };
            }
            catch (BusinessException exception)
            {
                return new SiteThemePreviewRenderResult { Error = ToError(exception) };
            }
        }
    }

    /// <summary>The exception's code and data, every value as invariant text.</summary>
    protected virtual SiteThemePreviewRenderError ToError(BusinessException exception)
    {
        return new SiteThemePreviewRenderError
        {
            Code = exception.Code ?? string.Empty,
            Data = exception.Data.Keys
                .OfType<string>()
                .ToDictionary(key => key, key => System.Convert.ToString(exception.Data[key], CultureInfo.InvariantCulture))
        };
    }
}
