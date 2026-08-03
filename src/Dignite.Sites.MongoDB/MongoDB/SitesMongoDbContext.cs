using Volo.Abp.Data;
using Volo.Abp.MongoDB;

namespace Dignite.Sites.MongoDB;

[ConnectionStringName(SitesDbProperties.ConnectionStringName)]
public class SitesMongoDbContext : AbpMongoDbContext, ISitesMongoDbContext
{
    /* Add mongo collections here. Example:
     * public IMongoCollection<Question> Questions => Collection<Question>();
     */

    protected override void CreateModel(IMongoModelBuilder modelBuilder)
    {
        base.CreateModel(modelBuilder);

        modelBuilder.ConfigureSites();
    }
}
