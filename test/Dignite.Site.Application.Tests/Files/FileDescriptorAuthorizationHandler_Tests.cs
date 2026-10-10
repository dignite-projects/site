using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Dignite.Site.Admin.Directories;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Directories;
using Dignite.Site.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using NSubstitute;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.BlobStoring;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// The resource-based rules behind every file-library call, against a container configured the way
/// <c>SiteAdminApplicationModule</c> configures Site's: these run without the test suite's always-allow
/// authorization, so a wrong rule shows up here.
/// </summary>
public class FileDescriptorAuthorizationHandler_Tests
{
    private const string Container = SiteFileContainerNames.Images;
    private static readonly Guid UploaderId = Guid.NewGuid();

    private readonly IPermissionChecker _permissionChecker = Substitute.For<IPermissionChecker>();
    private readonly HashSet<string> _granted = new();

    public FileDescriptorAuthorizationHandler_Tests()
    {
        _permissionChecker.IsGrantedAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>())
            .Returns(call => _granted.Contains(call.ArgAt<string>(1)));
    }

    [Fact]
    public async Task Create_Should_Be_Denied_Without_The_Create_Permission()
    {
        (await AuthorizeAsync(FileOperations.Create, CreateFile(creatorId: null), User())).ShouldBeFalse();
    }

    [Fact]
    public async Task Create_Should_Be_Granted_With_The_Containers_Create_Permission()
    {
        _granted.Add(SiteAdminPermissions.Contents.Create);

        (await AuthorizeAsync(FileOperations.Create, CreateFile(creatorId: null), User())).ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_Should_Be_Denied_To_Someone_Else_Without_The_Delete_Permission()
    {
        (await AuthorizeAsync(FileOperations.Delete, CreateFile(UploaderId), User(Guid.NewGuid()))).ShouldBeFalse();
    }

    [Fact]
    public async Task The_Uploader_May_Change_And_Delete_Their_Own_File()
    {
        var file = CreateFile(UploaderId);

        (await AuthorizeAsync(FileOperations.Update, file, User(UploaderId))).ShouldBeTrue();
        (await AuthorizeAsync(FileOperations.Delete, file, User(UploaderId))).ShouldBeTrue();
    }

    /// <summary>
    /// What File Explorer's "manage every file" permission became: whoever manages the site's contents
    /// manages the files they use, whoever uploaded them.
    /// </summary>
    [Fact]
    public async Task Contents_Management_Should_Grant_Every_Operation_On_Anyones_File()
    {
        _granted.Add(SiteAdminPermissions.Contents.Default);
        var file = CreateFile(UploaderId);

        foreach (var operation in new[] { FileOperations.Create, FileOperations.Update, FileOperations.Delete, FileOperations.Get })
        {
            (await AuthorizeAsync(operation, file, User(Guid.NewGuid()))).ShouldBeTrue(operation.Name);
        }
    }

    /// <summary>Site leaves the read permission unset: a descriptor is as public as the file it describes.</summary>
    [Fact]
    public async Task Get_Should_Be_Granted_To_Anyone_When_The_Container_Sets_No_Read_Permission()
    {
        (await AuthorizeAsync(FileOperations.Get, CreateFile(UploaderId), new ClaimsPrincipal(new ClaimsIdentity()))).ShouldBeTrue();
    }

    [Fact]
    public async Task Get_Should_Be_Denied_Without_A_Read_Permission_The_Container_Sets()
    {
        var file = CreateFile(UploaderId);

        (await AuthorizeAsync(FileOperations.Get, file, new ClaimsPrincipal(new ClaimsIdentity()), getPermission: "Files.Get")).ShouldBeFalse();
    }

    [Fact]
    public async Task Directories_Belong_To_Their_Owner()
    {
        var directory = new DirectoryDescriptor(Guid.NewGuid(), Container, "mine", null, 0, null) { CreatorId = UploaderId };
        var handler = new DirectoryDescriptorAuthorizationHandler(_permissionChecker, ConfigurationProvider(getPermission: null));

        (await AuthorizeAsync(handler, FileOperations.Update, directory, User(UploaderId))).ShouldBeTrue();
        (await AuthorizeAsync(handler, FileOperations.Delete, directory, User(Guid.NewGuid()))).ShouldBeFalse();

        // Even a Contents manager does not get into someone else's directories - they are personal.
        _granted.Add(SiteAdminPermissions.Contents.Default);
        (await AuthorizeAsync(handler, FileOperations.Get, directory, User(Guid.NewGuid()))).ShouldBeFalse();
    }

    [Fact]
    public async Task Creating_A_Directory_Needs_The_Containers_Directory_Permission()
    {
        var pending = new DirectoryDescriptor(Guid.NewGuid(), Container, "new", null, 0, null) { CreatorId = UploaderId };
        var handler = new DirectoryDescriptorAuthorizationHandler(_permissionChecker, ConfigurationProvider(getPermission: null));

        (await AuthorizeAsync(handler, FileOperations.Create, pending, User(UploaderId))).ShouldBeFalse();

        _granted.Add(SiteAdminPermissions.Contents.Create);
        (await AuthorizeAsync(handler, FileOperations.Create, pending, User(UploaderId))).ShouldBeTrue();
    }

    private Task<bool> AuthorizeAsync(
        OperationAuthorizationRequirement requirement,
        FileDescriptor file,
        ClaimsPrincipal user,
        string? getPermission = null)
    {
        return AuthorizeAsync(
            new FileDescriptorAuthorizationHandler(_permissionChecker, ConfigurationProvider(getPermission)),
            requirement,
            file,
            user);
    }

    private static async Task<bool> AuthorizeAsync(IAuthorizationHandler handler, OperationAuthorizationRequirement requirement, object resource, ClaimsPrincipal user)
    {
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, resource);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    /// <summary>The container as SiteAdminApplicationModule configures Site's, optionally with a read permission.</summary>
    private static IBlobContainerConfigurationProvider ConfigurationProvider(string? getPermission)
    {
        var configuration = new BlobContainerConfiguration();
        configuration.SetAuthorizationConfiguration(options =>
        {
            options.CreateFilePermissionName = SiteAdminPermissions.Contents.Create;
            options.UpdateFilePermissionName = SiteAdminPermissions.Contents.Update;
            options.DeleteFilePermissionName = SiteAdminPermissions.Contents.Delete;
            options.CreateDirectoryPermissionName = SiteAdminPermissions.Contents.Create;
            options.GetFilePermissionName = getPermission;
        });

        var provider = Substitute.For<IBlobContainerConfigurationProvider>();
        provider.Get(Container).Returns(configuration);
        return provider;
    }

    private static FileDescriptor CreateFile(Guid? creatorId)
    {
        return new FileDescriptor(
            Guid.NewGuid(), Container, "blob-name", "photo.png", "image/png", 1,
            hash: null, referBlobName: null, directoryId: null, tenantId: null)
        {
            CreatorId = creatorId
        };
    }

    private static ClaimsPrincipal User(Guid? userId = null)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(AbpClaimTypes.UserId, (userId ?? Guid.NewGuid()).ToString()) },
            authenticationType: "Test"));
    }
}
