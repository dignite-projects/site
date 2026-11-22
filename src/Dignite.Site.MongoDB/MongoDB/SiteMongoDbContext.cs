using Volo.Abp.Data;
using Volo.Abp.MongoDB;

namespace Dignite.Site.MongoDB;

[ConnectionStringName(SiteDbProperties.ConnectionStringName)]
public class SiteMongoDbContext : AbpMongoDbContext, ISiteMongoDbContext
{
    protected override void CreateModel(IMongoModelBuilder modelBuilder)
    {
        base.CreateModel(modelBuilder);

        modelBuilder.ConfigureSite();
    }
}
