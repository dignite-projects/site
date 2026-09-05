using Dignite.Abp.FlexFields;
using Dignite.FlexFields.Site.Localization;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.FlexFields.Site;

/// <summary>
/// Bolt-on module for Site's Content and Seo field types - same role as
/// <c>Dignite.Abp.FlexFields.FileExplorer</c>'s own module: each field type self-registers as
/// <c>IFieldType</c> via <c>ITransientDependency</c> the moment this assembly is scanned, so all this
/// module does is make that scan happen (<c>SiteDomainModule</c> depends on it) and register this
/// project's own localization resource.
/// <para>
/// Matrix and Table used to live here too (GitHub issue #49) until flex-fields shipped them as
/// kernel built-ins at <c>10.0.0-rc.16</c>, with the wire format unchanged - see abp-modules'
/// CHANGELOG. This project's own copies, and the <c>ICompositeFieldType</c>/<c>INormalizesValue</c>/
/// <c>InlineFieldDefinition</c>/<c>InlineFieldValidator</c>/<c>CompositeFieldNesting</c> contracts they
/// needed, were deleted in favor of the identical ones the kernel now provides.
/// </para>
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
