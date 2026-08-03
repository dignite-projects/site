using Localization.Resources.AbpUi;
using Dignite.Sites.Localization;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Dignite.Sites.Common;

[DependsOn(
    typeof(CommonApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule))]
public class CommonHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(CommonHttpApiModule).Assembly);
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Get<SitesResource>()
                .AddBaseTypes(typeof(AbpUiResource));
        });
    }
}
