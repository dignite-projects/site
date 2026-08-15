using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dignite.Site.Host.Data;

public class SiteHostDbContextFactory : IDesignTimeDbContextFactory<SiteHostDbContext>
{
    public SiteHostDbContext CreateDbContext(string[] args)
    {
        SiteHostGlobalFeatureConfigurator.Configure();
        SiteHostModuleExtensionConfigurator.Configure();

        SiteHostEfCoreEntityExtensionMappings.Configure();
        var configuration = BuildConfiguration();

        var builder = new DbContextOptionsBuilder<SiteHostDbContext>()
            .UseSqlite(configuration.GetConnectionString("Default"));

        return new SiteHostDbContext(builder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables();

        return builder.Build();
    }
}