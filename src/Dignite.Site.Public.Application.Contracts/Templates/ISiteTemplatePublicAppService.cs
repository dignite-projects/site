using System.Threading.Tasks;
using Dignite.Site.Templates;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Public.Templates;

public interface ISiteTemplatePublicAppService : IApplicationService
{
    /// <summary>
    /// Returns, in one query, the published version of the template called <paramref name="name"/> (null when
    /// the template is missing or unpublished, or when the name is a partial's or an asset's), the published
    /// <c>layout</c> template (null when there is none), every published <c>partials/*</c> template with its
    /// body, and the published version id of every <c>assets/*</c> template - so the renderer needs one
    /// call per page view. Only published versions are ever returned: a draft or an older version is not
    /// discoverable through the public surface.
    /// </summary>
    Task<SiteTemplateRenderSetDto> GetRenderSetAsync(string name);

    /// <summary>
    /// The published version of the template called <paramref name="name"/>, of any kind, or null when there
    /// is no such template or it has never been published. What <c>GET /site-assets/{path}</c> serves.
    /// </summary>
    Task<SiteTemplateVersionDto?> FindPublishedAsync(string name);
}
