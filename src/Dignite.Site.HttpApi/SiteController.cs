using Dignite.Site.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.Site;

public abstract class SiteController : AbpControllerBase
{
    protected SiteController()
    {
        LocalizationResource = typeof(SiteResource);
    }
}
