using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace Dignite.Sites;

[DependsOn(
    typeof(AbpVirtualFileSystemModule)
    )]
public class SitesInstallerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<SitesInstallerModule>();
        });
    }
}
