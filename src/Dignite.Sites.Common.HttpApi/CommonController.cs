using Dignite.Sites.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.Sites.Common;

public abstract class CommonController : AbpControllerBase
{
    protected CommonController()
    {
        LocalizationResource = typeof(SitesResource);
    }
}
