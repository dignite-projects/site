using Dignite.Site.Localization;
using Volo.Abp.Application.Services;

namespace Dignite.Site.Common;

public abstract class SiteCommonAppService : ApplicationService
{
    protected SiteCommonAppService()
    {
        LocalizationResource = typeof(SiteResource);
        ObjectMapperContext = typeof(SiteCommonApplicationModule);
    }
}
