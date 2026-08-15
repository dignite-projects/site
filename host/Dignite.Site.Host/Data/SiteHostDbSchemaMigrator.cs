using Volo.Abp.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Dignite.Site.Host.Data;

public class SiteHostDbSchemaMigrator : ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public SiteHostDbSchemaMigrator(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        
        /* We intentionally resolving the SiteHostDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
         */

        await _serviceProvider
            .GetRequiredService<SiteHostDbContext>()
            .Database
            .MigrateAsync();

    }
}
