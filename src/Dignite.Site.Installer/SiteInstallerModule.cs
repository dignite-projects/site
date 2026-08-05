using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.Site;

[DependsOn(
    typeof(AbpVirtualFileSystemModule)
    )]
public class SiteInstallerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<SiteInstallerModule>();
        });
    }
}
