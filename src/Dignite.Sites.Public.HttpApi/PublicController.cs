using Dignite.Sites.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.Sites.Public;

public abstract class PublicController : AbpControllerBase
{
    protected PublicController()
    {
        LocalizationResource = typeof(SitesResource);
    }
}
