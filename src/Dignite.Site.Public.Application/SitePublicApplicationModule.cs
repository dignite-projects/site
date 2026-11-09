using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
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
    // SkiaSharp, which ImageVariantRenderer draws with, and the SkiaSharpResizerOptions it reads.
    typeof(AbpImagingSkiaSharpModule)
    )]
public class SitePublicApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<SitePublicApplicationModule>();

        // ImageVariantRenderer's sampling and lossy quality. ABP's SkiaSharp default is nearest-neighbour
        // sampling, which wrecks a ?Width= downscale; the same values and the measurements behind them as
        // SiteAdminApplicationModule.ConfigureImaging.
        Configure<SkiaSharpResizerOptions>(options =>
        {
            options.SKSamplingOptions = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
            options.Quality = 85;
        });
    }
}
