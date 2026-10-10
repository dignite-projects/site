using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Files;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dignite.Site.Directories;

/// <summary>
/// The tree rules of <see cref="DirectoryManager"/>, against substituted repositories: no cycles, a parent in
/// the same container that belongs to the same owner, and deletion only when empty.
/// </summary>
public class DirectoryManager_Tests
{
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly IDirectoryDescriptorRepository _directories = Substitute.For<IDirectoryDescriptorRepository>();
    private readonly IFileDescriptorRepository _files = Substitute.For<IFileDescriptorRepository>();

    [Fact]
    public async Task MoveAsync_Should_Reject_Moving_A_Directory_Into_Itself()
    {
        var directory = CreateDirectory(null);
        _directories.GetAsync(directory.Id).Returns(directory);

        var exception = await Should.ThrowAsync<BusinessException>(() => CreateManager().MoveAsync(directory, directory.Id, 0));

        exception.Code.ShouldBe(SiteErrorCodes.DirectoryCannotMoveIntoItself);
    }

    [Fact]
    public async Task MoveAsync_Should_Reject_Moving_A_Directory_Into_A_Descendant()
    {
        var root = CreateDirectory(null);
        var child = CreateDirectory(root.Id);
        _directories.GetAsync(child.Id).Returns(child);
        _directories.GetAsync(root.Id).Returns(root);

        var exception = await Should.ThrowAsync<BusinessException>(() => CreateManager().MoveAsync(root, child.Id, 0));

        exception.Code.ShouldBe(SiteErrorCodes.DirectoryCannotMoveIntoItself);
    }

    [Fact]
    public async Task MoveAsync_Should_Reject_A_Parent_In_Another_Container()
    {
        var directory = CreateDirectory(null);
        var parent = CreateDirectory(null, SiteFileContainerNames.Default);
        _directories.GetAsync(parent.Id).Returns(parent);

        var exception = await Should.ThrowAsync<BusinessException>(() => CreateManager().MoveAsync(directory, parent.Id, 0));

        exception.Code.ShouldBe(SiteErrorCodes.DirectoryCannotMoveAcrossContainers);
    }

    [Fact]
    public async Task MoveAsync_Should_Reject_Another_Users_Directory_As_Parent()
    {
        var directory = CreateDirectory(null);
        var parent = CreateDirectory(null);
        parent.CreatorId = Guid.NewGuid();
        _directories.GetAsync(parent.Id).Returns(parent);

        var exception = await Should.ThrowAsync<BusinessException>(() => CreateManager().MoveAsync(directory, parent.Id, 0));

        exception.Code.ShouldBe(SiteErrorCodes.DirectoryNotFound);
    }

    [Fact]
    public async Task CreateAsync_Should_Reject_A_Missing_Parent()
    {
        var parentId = Guid.NewGuid();
        _directories.FindAsync(parentId, false).Returns((DirectoryDescriptor?)null);

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            CreateManager().CreateAsync(OwnerId, SiteFileContainerNames.Images, "child", parentId));

        exception.Code.ShouldBe(SiteErrorCodes.DirectoryNotFound);
    }

    [Fact]
    public async Task CreateAsync_Should_Reject_A_Parent_Of_Another_Owner_Or_Container()
    {
        var parent = new DirectoryDescriptor(Guid.NewGuid(), SiteFileContainerNames.Default, "parent", null, 0, null)
        {
            CreatorId = Guid.NewGuid()
        };
        _directories.FindAsync(parent.Id, false).Returns(parent);

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            CreateManager().CreateAsync(OwnerId, SiteFileContainerNames.Images, "child", parent.Id));

        exception.Code.ShouldBe(SiteErrorCodes.DirectoryNotFound);
    }

    /// <summary>The file library works only in Site's containers, never in another module's.</summary>
    [Fact]
    public async Task CreateAsync_Should_Reject_A_Container_That_Is_Not_Sites()
    {
        var exception = await Should.ThrowAsync<FileContainerNotAvailableException>(() =>
            CreateManager().CreateAsync(OwnerId, "invoices", "child"));

        exception.Code.ShouldBe(SiteErrorCodes.FileContainerNotAvailable);
    }

    [Fact]
    public async Task EnsureEmptyAsync_Should_Reject_A_Directory_With_Files()
    {
        var directory = CreateDirectory(null);
        _directories.GetListAsync(OwnerId, directory.ContainerName, directory.Id).Returns(new List<DirectoryDescriptor>());
        _files.GetListAsync(directory.ContainerName, null, directory.Id, maxResultCount: 1)
            .Returns(new List<FileDescriptor> { CreateFile(directory.Id) });

        var exception = await Should.ThrowAsync<DirectoryHasFilesException>(() => CreateManager().EnsureEmptyAsync(directory));

        exception.Code.ShouldBe(SiteErrorCodes.DirectoryHasFiles);
    }

    [Fact]
    public async Task EnsureEmptyAsync_Should_Reject_A_Directory_With_Subdirectories()
    {
        var directory = CreateDirectory(null);
        _directories.GetListAsync(OwnerId, directory.ContainerName, directory.Id)
            .Returns(new List<DirectoryDescriptor> { CreateDirectory(directory.Id) });
        _files.GetListAsync(directory.ContainerName, null, directory.Id, maxResultCount: 1).Returns(new List<FileDescriptor>());

        var exception = await Should.ThrowAsync<DirectoryHasChildrenException>(() => CreateManager().EnsureEmptyAsync(directory));

        exception.Code.ShouldBe(SiteErrorCodes.DirectoryHasChildren);
    }

    [Fact]
    public async Task DeleteAsync_Should_Detach_Soft_Deleted_Files_Then_Delete()
    {
        var directory = CreateDirectory(null);
        _directories.GetListAsync(OwnerId, directory.ContainerName, directory.Id).Returns(new List<DirectoryDescriptor>());
        _files.GetListAsync(directory.ContainerName, null, directory.Id, maxResultCount: 1).Returns(new List<FileDescriptor>());

        await CreateManager().DeleteAsync(directory);

        await _files.Received(1).ClearDirectoryFromDeletedFilesAsync(directory.Id, Arg.Any<CancellationToken>());
        await _directories.Received(1).DeleteAsync(directory, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    private DirectoryManager CreateManager()
    {
        var services = new ServiceCollection()
            .AddSingleton<IGuidGenerator>(SimpleGuidGenerator.Instance)
            .AddSingleton(Substitute.For<ICurrentTenant>())
            .BuildServiceProvider();

        return new DirectoryManager(_directories, _files)
        {
            LazyServiceProvider = new AbpLazyServiceProvider(services)
        };
    }

    private static DirectoryDescriptor CreateDirectory(Guid? parentId, string containerName = SiteFileContainerNames.Images)
    {
        var id = Guid.NewGuid();
        return new DirectoryDescriptor(id, containerName, id.ToString("N"), parentId, 0, null)
        {
            CreatorId = OwnerId
        };
    }

    private static FileDescriptor CreateFile(Guid? directoryId)
    {
        return new FileDescriptor(
            Guid.NewGuid(), SiteFileContainerNames.Images, Guid.NewGuid().ToString("N"), "file.png", "image/png", 1,
            hash: null, referBlobName: null, directoryId, tenantId: null);
    }
}
