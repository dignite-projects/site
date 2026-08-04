using Localization.Resources.AbpUi;
using Dignite.Sites.Localization;
using Dignite.Sites.Common;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Dignite.Sites.Admin;

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
                .Get<SitesResource>()
                .AddBaseTypes(typeof(AbpUiResource));
        });
    }
}
