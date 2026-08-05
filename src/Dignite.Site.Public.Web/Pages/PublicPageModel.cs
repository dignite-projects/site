using Dignite.Site.Localization;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;

namespace Dignite.Site.Public.Pages;

/* Inherit your PageModel classes from this class.
 */
public abstract class PublicPageModel : AbpPageModel
{
    protected PublicPageModel()
    {
        LocalizationResourceType = typeof(SiteResource);
        ObjectMapperContext = typeof(PublicWebModule);
    }
}
