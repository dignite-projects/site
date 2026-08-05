using Localization.Resources.AbpUi;
using Dignite.Site.Localization;
using Dignite.Site.Common;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Dignite.Site.Admin;

[DependsOn(
    typeof(AdminApplicationContractsModule),
    typeof(CommonHttpApiModule),
    typeof(AbpAspNetCoreMvcModule))]
public class AdminHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(AdminHttpApiModule).Assembly);
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
