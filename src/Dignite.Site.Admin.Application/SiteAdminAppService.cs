using Dignite.Site.Localization;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Admin;

public abstract class SiteAdminAppService : ApplicationService
{
    protected SiteAdminAppService()
    {
        LocalizationResource = typeof(SiteResource);
        ObjectMapperContext = typeof(SiteAdminApplicationModule);
    }
}
