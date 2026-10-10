using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.FileStoring;
using Dignite.Site.Admin.Directories;
using Dignite.Site.Admin.Files;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Directories;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Dignite.Site.Files;

/// <summary>
/// The admin API of the file library through its application services, against the real database and
/// filesystem container. Authorization always allows here (see FileDescriptorAuthorizationHandler_Tests for
/// the rules); these cover what the services do with the rows.
/// </summary>
public class FileAdminAppService_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly IFileAdminAppService _fileAppService;
    private readonly IDirectoryAdminAppService _directoryAppService;
    private readonly IDirectoryDescriptorRepository _directoryRepository;
    private readonly ICurrentTenant _currentTenant;

    public FileAdminAppService_Tests()
    {
        _fileAppService = GetRequiredService<IFileAdminAppService>();
        _directoryAppService = GetRequiredService<IDirectoryAdminAppService>();
        _directoryRepository = GetRequiredService<IDirectoryDescriptorRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task Should_Upload_Into_The_Callers_Own_Directory_And_List_It()
    {
        var directory = await _directoryAppService.CreateAsync(new CreateDirectoryInput
        {
            ContainerName = SiteFileContainerNames.Images,
            Name = "covers"
        });

        var file = await UploadAsync(SiteFileContainerNames.Images, "cover.png", TestFiles.Png(16, 16), directory.Id);

        file.DirectoryId.ShouldBe(directory.Id);
        file.MimeType.ShouldBe("image/png");
        // Relative to whichever host serves the site - the same for the HTTP API, the MCP tools and in-process callers.
        file.Url.ShouldBe($"/api/site-public/files/{SiteFileContainerNames.Images}/{file.BlobName}?__tenant={file.TenantId}");

        var listed = await _fileAppService.GetListAsync(new GetFilesInput
        {
            ContainerName = SiteFileContainerNames.Images,
            DirectoryId = directory.Id
        });
        listed.Items.ShouldHaveSingleItem().Id.ShouldBe(file.Id);

        var tree = await _directoryAppService.GetListAsync(new GetDirectoriesInput { ContainerName = SiteFileContainerNames.Images });
        tree.Items.ShouldContain(d => d.Id == directory.Id);
    }

    [Fact]
    public async Task Upload_Should_Refuse_A_Directory_Of_Another_Container()
    {
        var directory = await _directoryAppService.CreateAsync(new CreateDirectoryInput
        {
            ContainerName = SiteFileContainerNames.Default,
            Name = "documents"
        });

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            UploadAsync(SiteFileContainerNames.Images, "cover.png", TestFiles.Png(16, 16), directory.Id));

        exception.Code.ShouldBe(SiteErrorCodes.DirectoryNotFound);
    }

    [Fact]
    public async Task Upload_Should_Refuse_Another_Users_Directory()
    {
        var foreign = await WithUnitOfWorkAsync(() => _directoryRepository.InsertAsync(
            new DirectoryDescriptor(Guid.NewGuid(), SiteFileContainerNames.Images, "theirs", null, 0, _currentTenant.Id)
            {
                CreatorId = Guid.NewGuid()
            },
            autoSave: true));

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            UploadAsync(SiteFileContainerNames.Images, "cover.png", TestFiles.Png(16, 16), foreign.Id));

        exception.Code.ShouldBe(SiteErrorCodes.DirectoryNotFound);
    }

    [Fact]
    public async Task Upload_Should_Refuse_A_Container_That_Is_Not_Sites()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() =>
            UploadAsync("invoices", "a.pdf", TestFiles.Pdf()));

        exception.Code.ShouldBe(SiteErrorCodes.FileContainerNotAvailable);
    }

    [Fact]
    public async Task Update_Should_Rename_And_Move_Only_What_Was_Sent()
    {
        var directory = await _directoryAppService.CreateAsync(new CreateDirectoryInput
        {
            ContainerName = SiteFileContainerNames.Default,
            Name = "reports"
        });
        var file = await UploadAsync(SiteFileContainerNames.Default, "report.pdf", TestFiles.Pdf("rename"), directory.Id);

        var renamed = await _fileAppService.UpdateAsync(file.Id, new UpdateFileInput { Name = "annual-report.pdf" });
        renamed.Name.ShouldBe("annual-report.pdf");
        renamed.DirectoryId.ShouldBe(directory.Id);

        var moved = await _fileAppService.UpdateAsync(file.Id, new UpdateFileInput { DirectoryId = null });
        moved.DirectoryId.ShouldBeNull();
        moved.Name.ShouldBe("annual-report.pdf");
    }

    [Fact]
    public async Task Delete_Should_Remove_The_File()
    {
        var file = await UploadAsync(SiteFileContainerNames.Default, "gone.pdf", TestFiles.Pdf("gone"));

        await _fileAppService.DeleteAsync(file.Id);

        var listed = await _fileAppService.GetListAsync(new GetFilesInput { ContainerName = SiteFileContainerNames.Default });
        listed.Items.ShouldNotContain(f => f.Id == file.Id);
    }

    [Theory]
    [InlineData(SiteFileContainerNames.Images, 10)]
    [InlineData(SiteFileContainerNames.Default, 20)]
    public async Task Container_Configuration_Should_Report_What_The_Pipeline_Enforces(string containerName, int maxMb)
    {
        var configuration = await _fileAppService.GetContainerConfigurationAsync(containerName);

        configuration.MaxBlobSize.ShouldBe(maxMb * 1024L * 1024L);
        configuration.AllowedFileTypeNames.ShouldContain(".png");
        configuration.AllowedFileTypeNames.ShouldNotContain(".svg");
        configuration.CreateFilePermissionName.ShouldBe(SiteAdminPermissions.Contents.Create);
        configuration.GetFilePermissionName.ShouldBeNull();
    }

    [Fact]
    public async Task Directory_Delete_Should_Refuse_A_Directory_With_Files()
    {
        var directory = await _directoryAppService.CreateAsync(new CreateDirectoryInput
        {
            ContainerName = SiteFileContainerNames.Default,
            Name = "busy"
        });
        await UploadAsync(SiteFileContainerNames.Default, "busy.pdf", TestFiles.Pdf("busy"), directory.Id);

        var exception = await Should.ThrowAsync<BusinessException>(() => _directoryAppService.DeleteAsync(directory.Id));

        exception.Code.ShouldBe(SiteErrorCodes.DirectoryHasFiles);
    }

    [Fact]
    public async Task Directory_Move_Should_Refuse_Moving_Into_Its_Own_Child()
    {
        var parent = await _directoryAppService.CreateAsync(new CreateDirectoryInput { ContainerName = SiteFileContainerNames.Images, Name = "parent" });
        var child = await _directoryAppService.CreateAsync(new CreateDirectoryInput { ContainerName = SiteFileContainerNames.Images, Name = "child", ParentId = parent.Id });

        var exception = await Should.ThrowAsync<BusinessException>(() =>
            _directoryAppService.MoveAsync(parent.Id, new MoveDirectoryInput(child.Id, 0)));

        exception.Code.ShouldBe(SiteErrorCodes.DirectoryCannotMoveIntoItself);

        var renamed = await _directoryAppService.UpdateAsync(child.Id, new UpdateDirectoryInput { Name = "renamed" });
        renamed.Name.ShouldBe("renamed");
        (await _directoryAppService.GetListAsync(new GetDirectoriesInput { ContainerName = SiteFileContainerNames.Images }))
            .Items.Single(d => d.Id == parent.Id).Children.ShouldHaveSingleItem().Name.ShouldBe("renamed");
    }

    /// <summary>
    /// Directories belong to a user, so these run as one (the suite's other tests run without a user).
    /// </summary>
    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<ICurrentPrincipalAccessor, FakeCurrentPrincipalAccessor>());
    }

    private Task<FileDescriptorDto> UploadAsync(string containerName, string fileName, byte[] bytes, Guid? directoryId = null)
    {
        return _fileAppService.CreateAsync(new CreateFileInput
        {
            ContainerName = containerName,
            DirectoryId = directoryId,
            File = new RemoteStreamContent(new MemoryStream(bytes), fileName, "application/octet-stream")
        });
    }
}
