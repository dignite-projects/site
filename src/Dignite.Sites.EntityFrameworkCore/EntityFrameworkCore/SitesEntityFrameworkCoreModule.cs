using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Dignite.Sites.EntityFrameworkCore;

[DependsOn(
    typeof(SitesDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class SitesEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<SitesDbContext>(options =>
        {
            options.AddDefaultRepositories<ISitesDbContext>(includeAllEntities: true);
            
            /* Add custom repositories here. Example:
            * options.AddRepository<Question, EfCoreQuestionRepository>();
            */
        });
    }
}
