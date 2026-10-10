using Localization.Resources.AbpUi;
using Dignite.Site.Admin.Files;
using Dignite.Site.Localization;
using Dignite.Site.Common;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Dignite.Site.Admin;

[DependsOn(
    typeof(SiteAdminApplicationContractsModule),
    typeof(SiteCommonHttpApiModule),
    typeof(AbpAspNetCoreMvcModule))]
public class SiteAdminHttpApiModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddApplicationPartIfNotExists(typeof(SiteAdminHttpApiModule).Assembly);
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

        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            // An upload is multipart/form-data with the rest in the query string; without this ABP's
            // service convention would bind CreateFileInput from a JSON body instead.
            options.ConventionalControllers.FormBodyBindingIgnoredTypes.Add(typeof(CreateFileInput));
        });
    }
}
