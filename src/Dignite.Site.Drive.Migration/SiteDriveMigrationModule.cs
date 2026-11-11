using Dignite.Drive;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace Dignite.Site.DriveMigration;

/// <summary>
/// Moves the files of the file library Site kept before 3.0 into the Drive (<see cref="SiteDriveMigrator"/>), when the
/// host seeds its data. A host adds this module for the upgrade, with the Drive installed in the same process and
/// database, runs its data seeding (the dev Host's <c>--migrate-database</c>, a solution's DbMigrator), reads the report
/// in the log, sets <c>SiteDriveMigration:DryRun</c> to <c>false</c> and runs it again; then removes the module. See
/// <see cref="SiteDriveMigrationOptions"/>.
/// </summary>
[DependsOn(
    typeof(SiteDomainModule),
    typeof(DriveDomainModule)
)]
public class SiteDriveMigrationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<SiteDriveMigrationOptions>(context.Services.GetConfiguration().GetSection(SiteDriveMigrationOptions.ConfigurationSection));
    }
}
