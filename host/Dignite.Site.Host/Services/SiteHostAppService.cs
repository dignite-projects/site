using Volo.Abp.Application.Services;
using Dignite.Site.Host.Localization;

namespace Dignite.Site.Host.Services;

/* Inherit your application services from this class. */
public abstract class SiteHostAppService : ApplicationService
{
    protected SiteHostAppService()
    {
        LocalizationResource = typeof(SiteHostResource);
    }
}