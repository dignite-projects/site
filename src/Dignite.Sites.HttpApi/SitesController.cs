using Dignite.Sites.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.Sites;

public abstract class SitesController : AbpControllerBase
{
    protected SitesController()
    {
        LocalizationResource = typeof(SitesResource);
    }
}
