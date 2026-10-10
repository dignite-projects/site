using Dignite.Site.Directories;
using Dignite.Site.Files;
using MongoDB.Driver;
using Volo.Abp.Data;
using Volo.Abp.MongoDB;

namespace Dignite.Site.MongoDB;

[ConnectionStringName(SiteDbProperties.ConnectionStringName)]
public interface ISiteMongoDbContext : IAbpMongoDbContext
{
    IMongoCollection<DirectoryDescriptor> DirectoryDescriptors { get; }

    IMongoCollection<FileDescriptor> FileDescriptors { get; }
}
