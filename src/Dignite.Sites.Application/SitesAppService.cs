using Dignite.Sites.Localization;
using Volo.Abp.Application.Services;

namespace Dignite.Sites;

public abstract class SitesAppService : ApplicationService
{
    protected SitesAppService()
    {
        LocalizationResource = typeof(SitesResource);
        ObjectMapperContext = typeof(SitesApplicationModule);
    }
}
