using System.Collections.Generic;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Themes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Site.Admin.Themes;

/// <summary>
/// A deployment names the site's application in its configuration, as it names every remote service:
/// <c>RemoteServices:SitePublicWeb:BaseUrl</c> in appsettings.json is all the administration API needs.
/// </summary>
public class SiteThemeRemotePreviewRendererConfiguration_Tests : SiteEntityFrameworkCoreTestBase
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        base.SetAbpApplicationCreationOptions(options);

        options.Services.ReplaceConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RemoteServices:" + SiteThemePreviewConsts.RendererRemoteServiceName + ":BaseUrl"] = "http://site.internal:8080/"
            })
            .Build());
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.RemoveAll<ISiteThemePreviewRenderer>();
    }

    [Fact]
    public void The_Site_Should_Be_Read_From_The_Configuration()
    {
        GetRequiredService<SiteThemePreviewRendererProvider>().Find().ShouldBeOfType<SiteThemeRemotePreviewRenderer>();
    }
}
