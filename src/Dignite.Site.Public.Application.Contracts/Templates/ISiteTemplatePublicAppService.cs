using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Public.Templates;

public interface ISiteTemplatePublicAppService : IApplicationService
{
    /// <summary>
    /// Returns the published version of the template called <paramref name="name"/> (null when the
    /// template is missing or unpublished) and the published <c>layout</c> template (null when there is
    /// none), so the renderer needs one call per page view. Only published versions are ever returned - a
    /// draft or an older version is not discoverable through the public surface.
    /// </summary>
    Task<SiteTemplateRenderSetDto> GetRenderSetAsync(string name);
}
