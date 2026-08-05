using Dignite.Site.Localization;
using Volo.Abp.Application.Services;

namespace Dignite.Site;

public abstract class SiteAppService : ApplicationService
{
    protected SiteAppService()
    {
        LocalizationResource = typeof(SiteResource);
        ObjectMapperContext = typeof(SiteApplicationModule);
    }
}
