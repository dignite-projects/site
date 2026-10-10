using System;
using System.IO;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Files;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Xunit;

namespace Dignite.Site.Directories;

public class DirectoryManager_Integration_Tests : SiteEntityFrameworkCoreTestBase
{
    private static readonly Guid OwnerId = Guid.Parse("2e701e62-0953-4dd3-910b-dc6cc93ccb0d");

    private readonly DirectoryManager _directoryManager;
    private readonly FileDescriptorManager _fileDescriptorManager;
    private readonly IDirectoryDescriptorRepository _directoryRepository;
    private readonly IFileDescriptorRepository _fileRepository;
    private readonly IDataFilter _dataFilter;

    public DirectoryManager_Integration_Tests()
    {
        _directoryManager = GetRequiredService<DirectoryManager>();
        _fileDescriptorManager = GetRequiredService<FileDescriptorManager>();
        _directoryRepository = GetRequiredService<IDirectoryDescriptorRepository>();
        _fileRepository = GetRequiredService<IFileDescriptorRepository>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    /// <summary>
    /// A deleted file does not keep its directory alive: the directory goes, and the file's audit row
    /// stays, detached.
    /// </summary>
    [Fact]
    public async Task Delete_Should_Succeed_When_Only_Soft_Deleted_Files_Remain()
    {
        var directory = await WithUnitOfWorkAsync(() =>
            _directoryManager.CreateAsync(OwnerId, SiteFileContainerNames.Default, "to-delete-" + Guid.NewGuid().ToString("N")));
        var file = await WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(TestFiles.Pdf("in a directory " + Guid.NewGuid()));
            return await _fileDescriptorManager.CreateAsync(SiteFileContainerNames.Default, "a.pdf", stream, directory.Id);
        });
        await WithUnitOfWorkAsync(() => _fileDescriptorManager.DeleteAsync(file));

        await WithUnitOfWorkAsync(async () =>
            await _directoryManager.DeleteAsync(await _directoryRepository.GetAsync(directory.Id)));

        (await WithUnitOfWorkAsync(() => _directoryRepository.FindAsync(directory.Id))).ShouldBeNull();
        using (_dataFilter.Disable<ISoftDelete>())
        {
            (await WithUnitOfWorkAsync(() => _fileRepository.GetAsync(file.Id))).DirectoryId.ShouldBeNull();
        }
    }

    [Fact]
    public async Task Create_Should_Number_Siblings_And_Refuse_A_Duplicate_Name()
    {
        var name = "sibling-" + Guid.NewGuid().ToString("N");
        var first = await WithUnitOfWorkAsync(() => _directoryManager.CreateAsync(OwnerId, SiteFileContainerNames.Images, name + "-a"));
        var second = await WithUnitOfWorkAsync(() => _directoryManager.CreateAsync(OwnerId, SiteFileContainerNames.Images, name + "-b"));

        second.Order.ShouldBeGreaterThan(first.Order);
        first.CreatorId.ShouldBe(OwnerId);

        var exception = await Should.ThrowAsync<DirectoryNameAlreadyExistException>(() =>
            WithUnitOfWorkAsync(() => _directoryManager.CreateAsync(OwnerId, SiteFileContainerNames.Images, name + "-a")));
        exception.Code.ShouldBe(SiteErrorCodes.DirectoryNameAlreadyExists);
    }
}
