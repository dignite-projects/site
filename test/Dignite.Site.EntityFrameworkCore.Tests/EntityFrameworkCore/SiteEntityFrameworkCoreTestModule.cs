using Dignite.Drive;
using Dignite.Drive.EntityFrameworkCore;
using Dignite.Drive.Presets;
using Dignite.Site.DriveMigration;
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
    typeof(AbpBlobStoringFileSystemModule),
    // The Drive, where Site's files are kept, installed in this process as the dev Host installs it.
    typeof(DriveApplicationModule),
    typeof(DriveEntityFrameworkCoreModule),
    // The migration of the legacy file library to the Drive, as a host adds it for the upgrade. Its seed contributor
    // runs with the test data, in a dry run (the default): nothing is written until a test asks for it.
    typeof(SiteDriveMigrationModule)
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

        // What the host provides: the upload links are signed with it. Ephemeral keys, in memory, per test application.
        context.Services.AddDataProtection();

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
    /// Site's containers as the dev Host declares them: Drive user drives, read publicly, with the Drive's presets (the
    /// Host does it in the Drive section of appsettings.json), on the same provider kind (GitHub issue #41), pointed at
    /// a temp directory instead of App_Data so tests never touch a real deployment's files.
    /// </summary>
    private void ConfigureBlobStoring()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "dignite-site-tests", Guid.NewGuid().ToString("N"));

        Configure<AbpBlobStoringOptions>(options =>
        {
            foreach (var containerName in SiteFileContainerNames.All)
            {
                options.Containers.Configure(containerName, container =>
                {
                    container.UseFileSystem(fileSystem => fileSystem.BasePath = basePath);
                    container.UseDriveContainer(drive =>
                    {
                        drive.Kind = DriveContainerKind.UserDrive;
                        drive.PublicRead = true;
                    });
                });
            }

            options.Containers.Configure(SiteFileContainerNames.Images, container => container.UseImagesPreset());
            options.Containers.Configure(SiteFileContainerNames.Attachments, container => container.UseDocumentsPreset());
            options.Containers.Configure(SiteFileContainerNames.ThemeAssets, container => container.UseBinaryAssetsPreset());
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

        // SiteDbContext.OnModelCreating -> ConfigureSite() configures the legacy file library's entities too, so
        // this one call creates their tables as well.
        new SiteDbContext(
            new DbContextOptionsBuilder<SiteDbContext>().UseSqlite(connection).Options
        ).GetService<IRelationalDatabaseCreator>().CreateTables();

        // The Drive's own context, on the same connection: DriveFileDescriptors.
        new DriveDbContext(
            new DbContextOptionsBuilder<DriveDbContext>().UseSqlite(connection).Options
        ).GetService<IRelationalDatabaseCreator>().CreateTables();

        return connection;
    }
}
