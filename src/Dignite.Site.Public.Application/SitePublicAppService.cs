using Dignite.Site.Localization;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Public;

public abstract class SitePublicAppService : ApplicationService
{
    protected SitePublicAppService()
    {
        LocalizationResource = typeof(SiteResource);
        ObjectMapperContext = typeof(SitePublicApplicationModule);
    }
}
