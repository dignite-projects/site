using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Directories;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.Site.Files;

public class FileDescriptorRepository_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly IFileDescriptorRepository _fileDescriptorRepository;
    private readonly IDirectoryDescriptorRepository _directoryDescriptorRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    public FileDescriptorRepository_Tests()
    {
        _fileDescriptorRepository = GetRequiredService<IFileDescriptorRepository>();
        _directoryDescriptorRepository = GetRequiredService<IDirectoryDescriptorRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    [Fact]
    public async Task BlobNameExists_Should_Be_Tenant_Scoped()
    {
        var tenantId = Guid.NewGuid();
        var file = CreateFile("tenant-scope.txt", tenantId);

        using (_currentTenant.Change(tenantId))
        {
            await WithUnitOfWorkAsync(() => _fileDescriptorRepository.InsertAsync(file, autoSave: true));
            (await WithUnitOfWorkAsync(() => _fileDescriptorRepository.BlobNameExistsAsync(file.ContainerName, file.BlobName)))
                .ShouldBeTrue();
        }

        using (_currentTenant.Change(Guid.NewGuid()))
        {
            (await WithUnitOfWorkAsync(() => _fileDescriptorRepository.BlobNameExistsAsync(file.ContainerName, file.BlobName)))
                .ShouldBeFalse();
        }
    }

    [Fact]
    public async Task GetList_Should_Order_By_Creation_Time_Descending_By_Default()
    {
        var tenantId = Guid.NewGuid();
        using (_currentTenant.Change(tenantId))
        {
            var older = CreateFile("older", tenantId);
            older.CreationTime = DateTime.UtcNow.AddMinutes(-1);
            var newer = CreateFile("newer", tenantId);
            newer.CreationTime = DateTime.UtcNow;

            await WithUnitOfWorkAsync(async () =>
            {
                await _fileDescriptorRepository.InsertAsync(older, autoSave: true);
                await _fileDescriptorRepository.InsertAsync(newer, autoSave: true);
            });

            var result = await WithUnitOfWorkAsync(() =>
                _fileDescriptorRepository.GetListAsync(SiteFileContainerNames.Default, maxResultCount: 2));
            var count = await WithUnitOfWorkAsync(() =>
                _fileDescriptorRepository.GetCountAsync(SiteFileContainerNames.Default, filter: "e"));

            result.Select(f => f.Name).ShouldBe(new[] { "newer", "older" });
            count.ShouldBe(2);
        }
    }

    [Fact]
    public async Task ClearDirectoryFromDeletedFiles_Should_Detach_Only_Soft_Deleted_Files()
    {
        var directory = new DirectoryDescriptor(Guid.NewGuid(), SiteFileContainerNames.Default, "deleted-file-directory", null, 0, null)
        {
            CreatorId = Guid.NewGuid()
        };
        var deletedFile = CreateFile("deleted-file", null);
        deletedFile.MoveToDirectory(directory.Id);
        var activeFile = CreateFile("active-file", null);
        activeFile.MoveToDirectory(directory.Id);

        await WithUnitOfWorkAsync(async () =>
        {
            await _directoryDescriptorRepository.InsertAsync(directory, autoSave: true);
            await _fileDescriptorRepository.InsertAsync(deletedFile, autoSave: true);
            await _fileDescriptorRepository.DeleteAsync(deletedFile, autoSave: true);
            await _fileDescriptorRepository.InsertAsync(activeFile, autoSave: true);
        });

        await WithUnitOfWorkAsync(() => _fileDescriptorRepository.ClearDirectoryFromDeletedFilesAsync(directory.Id));

        (await WithUnitOfWorkAsync(() => _fileDescriptorRepository.GetAsync(activeFile.Id))).DirectoryId.ShouldBe(directory.Id);
        using (_dataFilter.Disable<ISoftDelete>())
        {
            var persisted = await WithUnitOfWorkAsync(() => _fileDescriptorRepository.GetAsync(deletedFile.Id));
            persisted.IsDeleted.ShouldBeTrue();
            persisted.DirectoryId.ShouldBeNull();
        }
    }

    private static FileDescriptor CreateFile(string name, Guid? tenantId)
    {
        return new FileDescriptor(
            Guid.NewGuid(), SiteFileContainerNames.Default, Guid.NewGuid().ToString("N"), name, "text/plain", 1,
            hash: Guid.NewGuid().ToString("N"), referBlobName: null, directoryId: null, tenantId);
    }
}
