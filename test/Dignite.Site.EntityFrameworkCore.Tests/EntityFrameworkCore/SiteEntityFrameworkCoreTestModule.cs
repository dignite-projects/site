using Dignite.Abp.FileStoring;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Files;
using Dignite.Site.Mcp;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.FileSystem;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace Dignite.Site.EntityFrameworkCore;

[DependsOn(
    typeof(SiteApplicationTestModule),
    typeof(SiteEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreSqliteModule),
    // The MCP tools, so they can be resolved and called directly. Nothing here starts an HTTP server -
    // the module's transport and endpoint registrations are inert without one, and the tools themselves
    // are plain services.
    typeof(SiteMcpModule),
    // The BLOB storage provider backing ConfigureBlobStoring below (GitHub issue #41) - same provider as
    // the dev Host, so this proves the same code path, not a test-only substitute.
    typeof(AbpBlobStoringFileSystemModule)
)]
public class SiteEntityFrameworkCoreTestModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<AbpSqliteOptions>(x => x.BusyTimeout = null);
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAlwaysDisableUnitOfWorkTransaction();

        var sqliteConnection = CreateDatabaseAndGetConnection();

        Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(configurationContext =>
            {
                configurationContext.UseSqlite(sqliteConnection);
            });
        });

        ConfigureBlobStoring();
    }

    /// <summary>
    /// The same container name, provider kind and authorization configuration (GitHub issue #41; the
    /// upload-permission wiring) as the dev Host, pointed at a temp directory instead of App_Data so tests
    /// never touch a real deployment's files. Kept in sync with SiteHostModule.ConfigureBlobStoring by hand -
    /// there is no shared source between the two, so a change to one needs the same change here.
    /// </summary>
    private void ConfigureBlobStoring()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "dignite-site-tests", Guid.NewGuid().ToString("N"));

        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.Configure(SiteFileContainerNames.Default, container =>
            {
                container.UseFileSystem(fileSystem =>
                {
                    fileSystem.BasePath = basePath;
                });

                container.SetAuthorizationConfiguration(config =>
                {
                    config.CreateFilePermissionName = SiteAdminPermissions.Contents.Create;
                });
            });
        });
    }

    /// <summary>
    /// Seeding lives here rather than in the shared <c>SiteTestBaseModule</c> - this is the layer that
    /// actually has a database, which is what every seed contributor touching a repository needs (see
    /// <c>SiteTestBaseModule</c>'s remarks).
    /// </summary>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        SeedTestData(context);
    }

    private static void SeedTestData(ApplicationInitializationContext context)
    {
        AsyncHelper.RunSync(async () =>
        {
            using (var scope = context.ServiceProvider.CreateScope())
            {
                await scope.ServiceProvider
                    .GetRequiredService<IDataSeeder>()
                    .SeedAsync();
            }
        });
    }

    private static SqliteConnection CreateDatabaseAndGetConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        // SiteDbContext.OnModelCreating -> ConfigureSite() now configures FileExplorer's entities too
        // (GitHub issue #41's follow-up - FileExplorer is part of Site, not a separate model), so this one
        // call creates its tables as well. FileDescriptorManager still resolves the default
        // FileExplorerDbContext class at runtime, sharing this same connection via the AbpDbContextOptions
        // default configured below - no [ReplaceDbContext] needed for that to work.
        new SiteDbContext(
            new DbContextOptionsBuilder<SiteDbContext>().UseSqlite(connection).Options
        ).GetService<IRelationalDatabaseCreator>().CreateTables();

        return connection;
    }
}
