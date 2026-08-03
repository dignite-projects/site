using Dignite.Sites.Localization;
using Volo.Abp.Application.Services;

namespace Dignite.Sites.Public;

public abstract class PublicAppService : ApplicationService
{
    protected PublicAppService()
    {
        LocalizationResource = typeof(SitesResource);
        ObjectMapperContext = typeof(PublicApplicationModule);
    }
}
