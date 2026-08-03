using Localization.Resources.AbpUi;
using Dignite.Sites.Localization;
using Dignite.Sites.Common;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Dignite.Sites.Public;

[DependsOn(
    typeof(PublicApplicationContractsModule),
    typeof(CommonHttpApiModule),
    typeof(AbpAspNetCoreMvcModule))]
public class PublicHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(PublicHttpApiModule).Assembly);
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
