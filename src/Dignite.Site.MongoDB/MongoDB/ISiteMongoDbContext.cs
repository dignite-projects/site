using Volo.Abp.Data;
using Volo.Abp.MongoDB;

namespace Dignite.Site.MongoDB;

[ConnectionStringName(SiteDbProperties.ConnectionStringName)]
public interface ISiteMongoDbContext : IAbpMongoDbContext
{
    /* Define mongo collections here. Example:
     * IMongoCollection<Question> Questions { get; }
     */
}
