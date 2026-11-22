using System.Threading.Tasks;
using Dignite.Abp.BlobManagement;
using Dignite.Abp.BlobManagement.Permissions;
using Dignite.Site.Files;
using Volo.Abp.Authorization.Permissions.Resources;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;

namespace Dignite.Site.Host.Data;

/// <summary>
/// Gives the <c>admin</c> role the directory permissions on Site's three file containers. Blob Management authorizes the
/// directories of a container by grants on the container alone - the file picker of a file field reads and changes them
/// with the user's own token - and not even the management permission stands in for the three that write. A role that
/// edits contents is granted them in the same way, in ABP's resource permission dialog (the <b>Permissions</b> button of a
/// container on Blob Management's Files page) or by <see cref="IResourcePermissionManager"/>.
/// </summary>
public class SiteFileDirectoryPermissionsDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private static readonly string[] Permissions =
    {
        BlobManagementPermissions.Container.View,
        BlobManagementPermissions.Container.CreateDirectories,
        BlobManagementPermissions.Container.UpdateDirectories,
        BlobManagementPermissions.Container.DeleteDirectories
    };

    protected IResourcePermissionManager ResourcePermissionManager { get; }

    protected ICurrentTenant CurrentTenant { get; }

    public SiteFileDirectoryPermissionsDataSeedContributor(
        IResourcePermissionManager resourcePermissionManager,
        ICurrentTenant currentTenant)
    {
        ResourcePermissionManager = resourcePermissionManager;
        CurrentTenant = currentTenant;
    }

    public virtual async Task SeedAsync(DataSeedContext context)
    {
        using (CurrentTenant.Change(context.TenantId))
        {
            foreach (var containerName in SiteFileContainerNames.All)
            {
                foreach (var permission in Permissions)
                {
                    await ResourcePermissionManager.SetAsync(
                        permission,
                        BlobManagementResourceNames.Container,
                        containerName,
                        RoleResourcePermissionValueProvider.ProviderName,
                        "admin",
                        true);
                }
            }
        }
    }
}
