using Dignite.Sites.Localization;
using Volo.Abp.Application.Services;

namespace Dignite.Sites.Common;

public abstract class CommonAppService : ApplicationService
{
    protected CommonAppService()
    {
        LocalizationResource = typeof(SitesResource);
        ObjectMapperContext = typeof(CommonApplicationModule);
    }
}
