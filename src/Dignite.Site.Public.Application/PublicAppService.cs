using Dignite.Site.Localization;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Public;

public abstract class PublicAppService : ApplicationService
{
    protected PublicAppService()
    {
        LocalizationResource = typeof(SiteResource);
        ObjectMapperContext = typeof(PublicApplicationModule);
    }
}
