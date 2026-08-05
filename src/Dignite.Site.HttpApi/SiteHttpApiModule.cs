using Localization.Resources.AbpUi;
using Dignite.Site.Localization;
using Dignite.Site.Admin;
using Dignite.Site.Public;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Dignite.Site;

[DependsOn(
    typeof(SiteApplicationContractsModule),
    typeof(AdminHttpApiModule),
    typeof(PublicHttpApiModule),
    typeof(AbpAspNetCoreMvcModule))]
public class SiteHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(SiteHttpApiModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Get<SiteResource>()
                .AddBaseTypes(typeof(AbpUiResource));
        });
    }
}
