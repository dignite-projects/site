using Localization.Resources.AbpUi;
using Dignite.Sites.Localization;
using Dignite.Sites.Admin;
using Dignite.Sites.Public;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Dignite.Sites;

[DependsOn(
    typeof(SitesApplicationContractsModule),
    typeof(AdminHttpApiModule),
    typeof(PublicHttpApiModule),
    typeof(AbpAspNetCoreMvcModule))]
public class SitesHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(SitesHttpApiModule).Assembly);
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
