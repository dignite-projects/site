using Dignite.Site.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace Dignite.Site.Public.Pages;

/* Inherit your PageModel classes from this class.
 */
public abstract class SitePublicPageModel : AbpPageModel
{
    protected SitePublicPageModel()
    {
        LocalizationResourceType = typeof(SiteResource);
        ObjectMapperContext = typeof(SitePublicWebModule);
    }
}
