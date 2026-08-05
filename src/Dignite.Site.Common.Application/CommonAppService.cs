using Dignite.Site.Localization;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Common;

public abstract class CommonAppService : ApplicationService
{
    protected CommonAppService()
    {
        LocalizationResource = typeof(SiteResource);
        ObjectMapperContext = typeof(CommonApplicationModule);
    }
}
