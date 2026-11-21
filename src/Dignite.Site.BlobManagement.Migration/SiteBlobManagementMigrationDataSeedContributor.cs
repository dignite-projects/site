using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Dignite.Site.BlobManagementMigration;

/// <summary>
/// Runs <see cref="SiteBlobManagementMigrator"/> when the host seeds its data, for the host and for each tenant it seeds.
/// </summary>
public class SiteBlobManagementMigrationDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    protected SiteBlobManagementMigrator Migrator { get; }

    protected ICurrentTenant CurrentTenant { get; }

    public SiteBlobManagementMigrationDataSeedContributor(SiteBlobManagementMigrator migrator, ICurrentTenant currentTenant)
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
