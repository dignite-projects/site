using Dignite.Site.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.Site.Common;

public abstract class SiteCommonController : AbpControllerBase
{
    protected SiteCommonController()
    {
        LocalizationResource = typeof(SiteResource);
    }
}
