using Dignite.Site.Localization;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Admin;

public abstract class AdminAppService : ApplicationService
{
    protected AdminAppService()
    {
        LocalizationResource = typeof(SiteResource);
        ObjectMapperContext = typeof(AdminApplicationModule);
    }
}
