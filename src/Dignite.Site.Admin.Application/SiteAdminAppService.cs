using Dignite.Site.Features;
using Dignite.Site.Localization;
using System.Threading;
using Volo.Abp.Application.Services;
using Volo.Abp.Features;
using Volo.Abp.Threading;

namespace Dignite.Site.Admin;

// On the base class so that every Admin application service - and the MCP tools, which only call these
// services through their interfaces - is gated at once. SiteAdminAppServiceFeature_Tests fails a new
// service that forgets to derive from this class. See SiteFeatures.Enable for why the Public services are
// deliberately left out.
[RequiresFeature(SiteFeatures.Enable)]
public abstract class SiteAdminAppService : ApplicationService
{
    /// <summary>The token of the request being served; none outside one.</summary>
    protected CancellationToken RequestCancellationToken =>
        LazyServiceProvider.LazyGetService<ICancellationTokenProvider>()?.Token ?? CancellationToken.None;

    protected SiteAdminAppService()
    {
        LocalizationResource = typeof(SiteResource);
        ObjectMapperContext = typeof(SiteAdminApplicationModule);
    }
}
