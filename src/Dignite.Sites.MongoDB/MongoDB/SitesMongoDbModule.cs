using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;
using Volo.Abp.MongoDB;

namespace Dignite.Sites.MongoDB;

[DependsOn(
    typeof(SitesDomainModule),
    typeof(AbpMongoDbModule)
    )]
public class SitesMongoDbModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMongoDbContext<SitesMongoDbContext>(options =>
        {
            options.AddDefaultRepositories<ISitesMongoDbContext>();
            
            /* Add custom repositories here. Example:
             * options.AddRepository<Question, MongoQuestionRepository>();
             */
        });
    }
}
