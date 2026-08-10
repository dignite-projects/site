using Localization.Resources.AbpUi;
using Dignite.FileExplorer;
using Dignite.Site.Localization;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Dignite.Site.Common;

[DependsOn(
    typeof(CommonApplicationContractsModule),
    typeof(AbpAspNetCoreMvcModule),
    // Dignite.FileExplorer's own controllers under api/file-explorer/* (GitHub issue #41's follow-up).
    typeof(FileExplorerHttpApiModule))]
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
                .Get<SiteResource>()
                .AddBaseTypes(typeof(AbpUiResource));
        });
    }
}
