using Volo.Abp.Data;
using Volo.Abp.MongoDB;

namespace Dignite.Sites.MongoDB;

[ConnectionStringName(SitesDbProperties.ConnectionStringName)]
public interface ISitesMongoDbContext : IAbpMongoDbContext
{
    /* Define mongo collections here. Example:
     * IMongoCollection<Question> Questions { get; }
     */
}
