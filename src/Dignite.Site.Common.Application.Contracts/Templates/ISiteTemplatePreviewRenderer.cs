using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Site.Templates;

/// <summary>
/// Renders a page of the site with one saved template version in place of the published one - what the
/// administration side calls to preview a version before publishing it.
/// <para>
/// Declared here, not in <c>Dignite.Site.Public.Application.Contracts</c>, because the caller is
/// <c>Dignite.Site.Admin.Application</c>, which does not reference the Public contracts, and the
/// implementation lives in <c>Dignite.Site.Public.Web</c>, which references nothing but them; this project is
/// what both tiers already share about templates. A plain interface rather than an application service: it is
/// an in-process hook, not an endpoint, so it gets no controller and no client proxy. An application without
/// <c>Dignite.Site.Public.Web</c> has no implementation, and a preview there fails with
/// <see cref="SiteErrorCodes.SiteTemplatePreviewUnavailable"/>.
/// </para>
/// </summary>
public interface ISiteTemplatePreviewRenderer
{
    /// <summary>
    /// The HTML <see cref="SiteTemplatePreviewRequest.Path"/> would be served with if
    /// <see cref="SiteTemplatePreviewRequest.Version"/> were the published version of its template.
    /// </summary>
    /// <exception cref="Volo.Abp.BusinessException">
    /// <see cref="SiteErrorCodes.SiteTemplatePreviewPathNotFound"/> when no page answers the path; also what a
    /// render that never called <c>site_head</c> raises (<see cref="SiteErrorCodes.SiteTemplateDidNotRenderSiteHead"/>).
    /// A template that fails at run time or runs out of time throws the renderer's own exception, which names
    /// the template, line and column.
    /// </exception>
    Task<SiteTemplatePreviewDto> RenderAsync(SiteTemplatePreviewRequest request, CancellationToken cancellationToken = default);
}
