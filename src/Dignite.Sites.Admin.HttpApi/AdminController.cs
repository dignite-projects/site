using Dignite.Sites.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.Sites.Admin;

public abstract class AdminController : AbpControllerBase
{
    protected AdminController()
    {
        LocalizationResource = typeof(SitesResource);
    }
}
