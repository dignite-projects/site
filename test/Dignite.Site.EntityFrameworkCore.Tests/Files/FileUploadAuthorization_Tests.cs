using Dignite.Abp.FileStoring;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.BlobStoring;
using Xunit;

namespace Dignite.Site.Files;

/// <summary>
/// Not an authorization-behaviour test - <c>SiteTestBaseModule.AddAlwaysAllowAuthorization</c> makes
/// every permission check in this whole suite succeed regardless of what is configured, so a live upload
/// call here would pass whether or not <c>site-files</c> is wired correctly and would not have caught a
/// wrong or missing permission name. Whether <c>CreateFilePermissionName</c> actually gates
/// <c>FileDescriptorAppService.CreateAsync</c> is FileExplorer's own mechanism, already covered by that
/// repo's <c>FileDescriptorAuthorizationHandler_Tests</c>.
/// <para>
/// What this proves instead is narrower: that <b>Site's own</b> module wiring names the right permission
/// for the right container - a plain configuration read, immune to the always-allow override because it
/// never performs a check.
/// </para>
/// </summary>
public class FileUploadAuthorization_Tests : SiteEntityFrameworkCoreTestBase
{
    [Fact]
    public void Site_Files_Container_Requires_ContentCreate_To_Upload_And_Stays_Publicly_Readable()
    {
        var containerConfiguration = GetRequiredService<IBlobContainerConfigurationProvider>()
            .Get(SiteFileContainerNames.Default);
        var authorization = containerConfiguration.GetAuthorizationConfiguration();

        authorization.CreateFilePermissionName.ShouldBe(SiteAdminPermissions.Contents.Create);

        // Unset, not locked down: a published content's images must load for anonymous site visitors, not
        // just authenticated editors (FileDescriptorAuthorizationHandler's own default for unset is
        // "everyone may read").
        authorization.GetFilePermissionName.ShouldBeNull();
    }
}
