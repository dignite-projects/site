using Dignite.Site.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.Site.Admin;

public abstract class SiteAdminController : AbpControllerBase
{
    protected SiteAdminController()
    {
        LocalizationResource = typeof(SiteResource);
    }
}
