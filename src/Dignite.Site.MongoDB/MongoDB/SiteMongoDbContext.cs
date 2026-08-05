using Volo.Abp.Data;
using Volo.Abp.MongoDB;

namespace Dignite.Site.MongoDB;

[ConnectionStringName(SiteDbProperties.ConnectionStringName)]
public class SiteMongoDbContext : AbpMongoDbContext, ISiteMongoDbContext
{
    /* Add mongo collections here. Example:
     * public IMongoCollection<Question> Questions => Collection<Question>();
     */

    protected override void CreateModel(IMongoModelBuilder modelBuilder)
    {
        base.CreateModel(modelBuilder);

        modelBuilder.ConfigureSite();
    }
}
