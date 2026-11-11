using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.DriveMigration;

/// <summary>
/// Runs <see cref="SiteDriveMigrator"/> when the host seeds its data, for the host and for each tenant it seeds.
/// </summary>
public class SiteDriveMigrationDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    protected SiteDriveMigrator Migrator { get; }

    protected ICurrentTenant CurrentTenant { get; }

    public SiteDriveMigrationDataSeedContributor(SiteDriveMigrator migrator, ICurrentTenant currentTenant)
    {
        Migrator = migrator;
        CurrentTenant = currentTenant;
    }

    public virtual async Task SeedAsync(DataSeedContext context)
    {
        using (CurrentTenant.Change(context.TenantId))
        {
            await Migrator.MigrateAsync();
        }
    }
}
