using Dignite.Site.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.Site.Public;

public abstract class SitePublicController : AbpControllerBase
{
    protected SitePublicController()
    {
        LocalizationResource = typeof(SiteResource);
    }
}
