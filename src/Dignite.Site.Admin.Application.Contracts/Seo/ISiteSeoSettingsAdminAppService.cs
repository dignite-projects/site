using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Admin.Seo;

/// <summary>
/// The site's SEO settings: what the head of a page falls back to when its content says nothing - for now the default
/// share image. Read and set for the current tenant, or, on the host's own site, as the default of every site that sets
/// none, as the languages are.
/// </summary>
public interface ISiteSeoSettingsAdminAppService : IApplicationService
{
    Task<SiteSeoSettingsDto> GetAsync();

    Task<SiteSeoSettingsDto> UpdateAsync(UpdateSiteSeoSettingsDto input);
}
