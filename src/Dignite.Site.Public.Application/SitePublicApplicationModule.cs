using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Imaging;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.Application;
using Dignite.Abp.BlobStoring.Imaging;
using Dignite.Site.Common;

namespace Dignite.Site.Public;

[DependsOn(
    typeof(SiteDomainModule),
    typeof(SitePublicApplicationContractsModule),
    typeof(SiteCommonApplicationModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule),
    // The localization of the Dignite.Abp.BlobStoring.Imaging error codes the public read endpoint's
    // ?Width=&Height= resize reports.
    typeof(AbpBlobStoringImagingModule),
    // IImageResizer (SkiaSharp) for that resize.
    typeof(AbpImagingSkiaSharpModule)
    )]
public class SitePublicApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<SitePublicApplicationModule>();
    }
}
