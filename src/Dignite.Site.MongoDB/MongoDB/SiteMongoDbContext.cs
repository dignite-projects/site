using Dignite.Site.Directories;
using Dignite.Site.Files;
using MongoDB.Driver;
using Volo.Abp.Data;
using Volo.Abp.MongoDB;

namespace Dignite.Site.MongoDB;

[ConnectionStringName(SiteDbProperties.ConnectionStringName)]
public class SiteMongoDbContext : AbpMongoDbContext, ISiteMongoDbContext
{
    public IMongoCollection<DirectoryDescriptor> DirectoryDescriptors => Collection<DirectoryDescriptor>();

    public IMongoCollection<FileDescriptor> FileDescriptors => Collection<FileDescriptor>();

    protected override void CreateModel(IMongoModelBuilder modelBuilder)
    {
        base.CreateModel(modelBuilder);

        modelBuilder.ConfigureSite();
    }
}
