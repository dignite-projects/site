using Dignite.Site.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.Site.Common;

public abstract class CommonController : AbpControllerBase
{
    protected CommonController()
    {
        LocalizationResource = typeof(SiteResource);
    }
}
