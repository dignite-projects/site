using Volo.Abp.Application.Services;
using Dignite.Sites.Host.Localization;

namespace Dignite.Sites.Host.Services;

/* Inherit your application services from this class. */
public abstract class HostAppService : ApplicationService
{
    protected HostAppService()
    {
        LocalizationResource = typeof(HostResource);
    }
}