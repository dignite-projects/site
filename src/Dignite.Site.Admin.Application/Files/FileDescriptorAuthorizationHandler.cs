using System;
using System.Security.Principal;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.BlobStoring;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// Authorizes one <see cref="FileOperations"/> requirement on a <see cref="FileDescriptor"/>. Any of these
/// grants it:
/// <list type="bullet">
/// <item>reading (<see cref="FileOperations.Get"/>) a file in a container with no read permission set;</item>
/// <item>being the file's uploader;</item>
/// <item>holding the permission the container sets for the operation
/// (<see cref="BlobContainerAuthorizationConfiguration"/>);</item>
/// <item>holding <see cref="SiteAdminPermissions.Contents.Default"/> - what File Explorer's separate
/// "manage every file" permission became: whoever may manage the site's contents may manage the files those
/// contents use.</item>
/// </list>
/// </summary>
public class FileDescriptorAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, FileDescriptor>
{
    protected IPermissionChecker PermissionChecker { get; }

    protected IBlobContainerConfigurationProvider BlobContainerConfigurationProvider { get; }

    public FileDescriptorAuthorizationHandler(
        IPermissionChecker permissionChecker,
        IBlobContainerConfigurationProvider blobContainerConfigurationProvider)
    {
        PermissionChecker = permissionChecker;
        BlobContainerConfigurationProvider = blobContainerConfigurationProvider;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        FileDescriptor resource)
    {
        var authorization = BlobContainerConfigurationProvider.Get(resource.ContainerName).GetAuthorizationConfiguration();
        var permissionName = GetPermissionName(authorization, requirement);

        if ((permissionName.IsNullOrEmpty() && requirement.Name == FileOperations.Get.Name)
            || (resource.CreatorId.HasValue && resource.CreatorId == context.User.FindUserId())
            || (!permissionName.IsNullOrEmpty() && await PermissionChecker.IsGrantedAsync(context.User, permissionName))
            || await PermissionChecker.IsGrantedAsync(context.User, SiteAdminPermissions.Contents.Default))
        {
            context.Succeed(requirement);
        }
    }

    protected virtual string? GetPermissionName(
        BlobContainerAuthorizationConfiguration authorization,
        OperationAuthorizationRequirement requirement)
    {
        return requirement.Name switch
        {
            nameof(FileOperations.Create) => authorization.CreateFilePermissionName,
            nameof(FileOperations.Update) => authorization.UpdateFilePermissionName,
            nameof(FileOperations.Delete) => authorization.DeleteFilePermissionName,
            nameof(FileOperations.Get) => authorization.GetFilePermissionName,
            _ => null
        };
    }
}
