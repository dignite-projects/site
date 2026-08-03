using Dignite.Sites.Localization;
using Volo.Abp.Application.Services;

namespace Dignite.Sites.Admin;

public abstract class AdminAppService : ApplicationService
{
    protected AdminAppService()
    {
        LocalizationResource = typeof(SitesResource);
        ObjectMapperContext = typeof(AdminApplicationModule);
    }
}
