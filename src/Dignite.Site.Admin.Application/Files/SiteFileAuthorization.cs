using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Files;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// Which of Site's permissions let a user put files in, and take them out of, each container of
/// <see cref="SiteFileContainerNames"/>. Blob Management has no user in front of it, so the rule is Site's, as the
/// uploads of the contents it belongs to are:
/// <list type="bullet">
/// <item><c>images</c> and <c>attachments</c> take the files of a content (<c>SiteAdmin.Contents.Create</c> or
/// <c>.Update</c>); <c>images</c> also those of the SEO settings (<c>SiteAdmin.Seo.Update</c>). Whoever deletes
/// contents deletes their files;</item>
/// <item><c>theme-assets</c> takes the files a theme links, with <c>SiteAdmin.Themes.Edit</c>.</item>
/// </list>
/// A user who uploaded a file may always delete it again while they may still upload to its container: an upload the
/// form that asked for it did not keep is cleaned up that way.
/// </summary>
public class SiteFileAuthorization : ITransientDependency
{
    protected IPermissionChecker PermissionChecker { get; }

    public SiteFileAuthorization(IPermissionChecker permissionChecker)
    {
        PermissionChecker = permissionChecker;
    }

    /// <summary>Whether <paramref name="containerName"/> is one of Site's, and the caller may upload to it.</summary>
    public virtual async Task<bool> CanUploadAsync(string containerName)
    {
        foreach (var permission in UploadPermissions(containerName))
        {
            if (await PermissionChecker.IsGrantedAsync(permission))
            {
                return true;
            }
        }

        return false;
    }

    public virtual async Task CheckUploadAsync(string containerName)
    {
        if (!await CanUploadAsync(containerName))
        {
            throw new AbpAuthorizationException();
        }
    }

    /// <summary>
    /// Whether the caller may delete a file of <paramref name="containerName"/> that <paramref name="creatorId"/>
    /// uploaded.
    /// </summary>
    public virtual async Task<bool> CanDeleteAsync(string containerName, Guid? creatorId, Guid? currentUserId)
    {
        if (!await CanUploadAsync(containerName))
        {
            return false;
        }

        if (creatorId.HasValue && creatorId == currentUserId)
        {
            return true;
        }

        return await PermissionChecker.IsGrantedAsync(DeletePermission(containerName));
    }

    protected virtual IEnumerable<string> UploadPermissions(string containerName)
    {
        switch (containerName)
        {
            case SiteFileContainerNames.Images:
                yield return SiteAdminPermissions.Contents.Create;
                yield return SiteAdminPermissions.Contents.Update;
                yield return SiteAdminPermissions.Seo.Update;
                break;
            case SiteFileContainerNames.Attachments:
                yield return SiteAdminPermissions.Contents.Create;
                yield return SiteAdminPermissions.Contents.Update;
                break;
            case SiteFileContainerNames.ThemeAssets:
                yield return SiteAdminPermissions.Themes.Edit;
                break;
        }
    }

    protected virtual string DeletePermission(string containerName)
    {
        return containerName == SiteFileContainerNames.ThemeAssets
            ? SiteAdminPermissions.Themes.Edit
            : SiteAdminPermissions.Contents.Delete;
    }
}
