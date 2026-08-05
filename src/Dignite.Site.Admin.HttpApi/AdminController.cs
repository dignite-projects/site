using Dignite.Site.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.Site.Admin;

public abstract class AdminController : AbpControllerBase
{
    protected AdminController()
    {
        LocalizationResource = typeof(SiteResource);
    }
}
