using Dignite.Site.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.Site.Public;

public abstract class PublicController : AbpControllerBase
{
    protected PublicController()
    {
        LocalizationResource = typeof(SiteResource);
    }
}
