using System;
using System.Security.Principal;
using System.Threading.Tasks;
using Dignite.Site.Admin.Files;
using Dignite.Site.Directories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.BlobStoring;

namespace Dignite.Site.Admin.Directories;

/// <summary>
/// Directories are personal: only the owner may read, rename, move or delete one. Creating one needs the
/// container's <see cref="BlobContainerAuthorizationConfiguration.CreateDirectoryPermissionName"/> - unset
/// means nobody may.
/// </summary>
public class DirectoryDescriptorAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, DirectoryDescriptor>
{
    protected IPermissionChecker PermissionChecker { get; }

    protected IBlobContainerConfigurationProvider BlobContainerConfigurationProvider { get; }

    public DirectoryDescriptorAuthorizationHandler(
        IPermissionChecker permissionChecker,
        IBlobContainerConfigurationProvider blobContainerConfigurationProvider)
    {
        PermissionChecker = permissionChecker;
        BlobContainerConfigurationProvider = blobContainerConfigurationProvider;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        DirectoryDescriptor resource)
    {
        if (requirement.Name == FileOperations.Create.Name)
        {
            var permissionName = BlobContainerConfigurationProvider
                .Get(resource.ContainerName)
                .GetAuthorizationConfiguration()
                .CreateDirectoryPermissionName;

            if (!permissionName.IsNullOrEmpty() && await PermissionChecker.IsGrantedAsync(context.User, permissionName))
            {
                context.Succeed(requirement);
            }

            return;
        }

        if (resource.CreatorId != null && resource.CreatorId == context.User.FindUserId())
        {
            context.Succeed(requirement);
        }
    }
}
