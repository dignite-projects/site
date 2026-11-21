using Volo.Abp.Modularity;
using Dignite.Abp.BlobManagement;
using Volo.Abp.Localization;
using Dignite.Site.Localization;
using Volo.Abp.Domain;
using Volo.Abp.Localization.ExceptionHandling;
using Volo.Abp.Validation;
using Volo.Abp.Validation.Localization;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.Site;

[DependsOn(
    typeof(AbpValidationModule),
    typeof(AbpDddDomainSharedModule),
    typeof(BlobManagementDomainSharedModule)
)]
public class SiteDomainSharedModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<SiteDomainSharedModule>();
        });

        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Add<SiteResource>("en")
                .AddBaseTypes(typeof(AbpValidationResource))
                .AddVirtualJson("/Localization/Site");
        });

        Configure<AbpExceptionLocalizationOptions>(options =>
        {
            options.MapCodeNamespace("Site", typeof(SiteResource));
        });
    }
}
