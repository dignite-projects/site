using Dignite.Site.Directories;
using Dignite.Site.Files;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;
using Volo.Abp.MongoDB;

namespace Dignite.Site.MongoDB;

[DependsOn(
    typeof(SiteDomainModule),
    typeof(AbpMongoDbModule)
    )]
public class SiteMongoDbModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMongoDbContext<SiteMongoDbContext>(options =>
        {
            options.AddDefaultRepositories<ISiteMongoDbContext>();

            options.AddRepository<FileDescriptor, MongoFileDescriptorRepository>();
            options.AddRepository<DirectoryDescriptor, MongoDirectoryDescriptorRepository>();
        });
    }
}
