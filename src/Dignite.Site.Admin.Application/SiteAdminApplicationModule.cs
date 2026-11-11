using Dignite.Site.Admin.Themes;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.RemoteServices;
using Volo.Abp.Application;
using Dignite.Site.Common;

namespace Dignite.Site.Admin;

/// <summary>
/// Site's administration application services. Files are not among them: Site's files are the Drive's
/// (<c>Dignite.Drive.*</c>, installed by the host), uploaded and deleted through the Drive's own API and permissions,
/// and the containers Site's fields use - <c>images</c>, <c>attachments</c>, <c>theme-assets</c> - are declared by the
/// host as Drive containers.
/// </summary>
[DependsOn(
    typeof(SiteDomainModule),
    typeof(SiteAdminApplicationContractsModule),
    typeof(SiteCommonApplicationModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule),
    // The lock a theme link is recorded as used under (SiteThemeTransferTicketProtector): one implementation per
    // process by default, a distributed one when the host configures it.
    typeof(AbpDistributedLockingAbstractionsModule),
    // RemoteServices:SitePublicWeb, the site's application, which renders the previews of an administration API
    // deployed apart from it (SiteThemeRemotePreviewRenderer).
    typeof(AbpRemoteServicesModule)
    )]
public class SiteAdminApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<SiteAdminApplicationModule>();

        // The calls of SiteThemeRemotePreviewRenderer. A render has its own time limit on the site; this one only
        // ends a call to a site that does not answer.
        context.Services.AddHttpClient(SiteThemeRemotePreviewRenderer.HttpClientName, client =>
        {
            client.Timeout = System.TimeSpan.FromMinutes(1);
        });
    }
}
