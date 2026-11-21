using Dignite.Abp.BlobManagement;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace Dignite.Site.BlobManagementMigration;

/// <summary>
/// Moves the files of the file library Site kept before 3.0 into Blob Management (<see cref="SiteBlobManagementMigrator"/>), when the
/// host seeds its data. A host adds this module for the upgrade, with Blob Management installed in the same process and
/// database, runs its data seeding (the dev Host's <c>--migrate-database</c>, a solution's DbMigrator), reads the report
/// in the log, sets <c>SiteBlobManagementMigration:DryRun</c> to <c>false</c> and runs it again; then removes the module. See
/// <see cref="SiteBlobManagementMigrationOptions"/>.
/// </summary>
[DependsOn(
    typeof(SiteDomainModule),
    typeof(BlobManagementDomainModule)
)]
public class SiteBlobManagementMigrationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<SiteBlobManagementMigrationOptions>(context.Services.GetConfiguration().GetSection(SiteBlobManagementMigrationOptions.ConfigurationSection));
    }
}
