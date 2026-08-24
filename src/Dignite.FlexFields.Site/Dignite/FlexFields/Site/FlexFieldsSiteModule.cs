using Dignite.Abp.FlexFields;
using Dignite.FlexFields.Site.Localization;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.FlexFields.Site;

/// <summary>
/// Bolt-on module for Site's Content, Matrix and Table field types (plus, once moved here, Seo) - same
/// role as <c>Dignite.Abp.FlexFields.FileExplorer</c>'s own module: each field type self-registers as
/// <c>IFieldType</c> via <c>ITransientDependency</c> the moment this assembly is scanned, so all this
/// module does is make that scan happen (<c>SiteDomainModule</c> depends on it) and register this
/// project's own localization resource.
/// </summary>
[DependsOn(
    typeof(FlexFieldsAbstractionsModule)
    )]
public class FlexFieldsSiteModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<FlexFieldsSiteModule>();
        });

        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Add<FlexFieldsSiteResource>("en")
                .AddVirtualJson("/Dignite/FlexFields/Site/Localization/Resources");
        });
    }
}
