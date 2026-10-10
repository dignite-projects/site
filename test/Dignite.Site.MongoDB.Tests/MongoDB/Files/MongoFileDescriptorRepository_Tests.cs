using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Directories;
using Dignite.Site.Files;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Xunit;

namespace Dignite.Site.MongoDB.Files;

/// <summary>
/// The file library's two repositories on MongoDB - the same queries the EF Core ones answer, so a Site
/// deployment on MongoDB gets the same file library.
/// </summary>
[Collection(MongoTestCollection.Name)]
public class MongoFileDescriptorRepository_Tests : SiteMongoDbTestBase
{
    private readonly IFileDescriptorRepository _files;
    private readonly IDirectoryDescriptorRepository _directories;
    private readonly IDataFilter _dataFilter;

    public MongoFileDescriptorRepository_Tests()
    {
        _files = GetRequiredService<IFileDescriptorRepository>();
        _directories = GetRequiredService<IDirectoryDescriptorRepository>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    [Fact]
    public async Task Should_Find_Files_By_Blob_Name_Hash_And_Reference()
    {
        var owner = CreateFile("owner.pdf", hash: new string('A', 64));
        var reference = CreateFile("reference.pdf", hash: null, referBlobName: owner.BlobName);
        await WithUnitOfWorkAsync(async () =>
        {
            await _files.InsertAsync(owner, autoSave: true);
            await _files.InsertAsync(reference, autoSave: true);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            (await _files.BlobNameExistsAsync(SiteFileContainerNames.Default, owner.BlobName)).ShouldBeTrue();
            (await _files.FindByBlobNameAsync(SiteFileContainerNames.Default, reference.BlobName))!.Id.ShouldBe(reference.Id);
            (await _files.FindByHashAsync(SiteFileContainerNames.Default, owner.Hash))!.Id.ShouldBe(owner.Id);
            (await _files.FindByHashAsync(SiteFileContainerNames.Default, string.Empty)).ShouldBeNull();
            (await _files.ReferencingAnyAsync(SiteFileContainerNames.Default, owner.BlobName)).ShouldBeTrue();
            (await _files.ReferencingAnyAsync(SiteFileContainerNames.Images, owner.BlobName)).ShouldBeFalse();
        });
    }

    [Fact]
    public async Task GetList_Should_Filter_Count_And_Order_Newest_First()
    {
        var older = CreateFile("older.pdf", hash: null);
        older.CreationTime = DateTime.UtcNow.AddMinutes(-1);
        var newer = CreateFile("newer.pdf", hash: null);
        newer.CreationTime = DateTime.UtcNow;
        var other = CreateFile("other.png", hash: null, containerName: SiteFileContainerNames.Images);
        await WithUnitOfWorkAsync(async () =>
        {
            await _files.InsertAsync(older, autoSave: true);
            await _files.InsertAsync(newer, autoSave: true);
            await _files.InsertAsync(other, autoSave: true);
        });

        await WithUnitOfWorkAsync(async () =>
        {
            (await _files.GetListAsync(SiteFileContainerNames.Default)).Select(f => f.Name).ShouldBe(new[] { "newer.pdf", "older.pdf" });
            (await _files.GetCountAsync(SiteFileContainerNames.Default, filter: "old")).ShouldBe(1);
            (await _files.GetListAsync(SiteFileContainerNames.Default, sorting: "Name asc")).First().Name.ShouldBe("newer.pdf");
        });
    }

    [Fact]
    public async Task ClearDirectoryFromDeletedFiles_Should_Detach_Only_Soft_Deleted_Files()
    {
        var ownerId = Guid.NewGuid();
        var directory = new DirectoryDescriptor(Guid.NewGuid(), SiteFileContainerNames.Default, "dir", null, 0, null) { CreatorId = ownerId };
        var deleted = CreateFile("deleted.pdf", hash: null);
        deleted.MoveToDirectory(directory.Id);
        var active = CreateFile("active.pdf", hash: null);
        active.MoveToDirectory(directory.Id);

        await WithUnitOfWorkAsync(async () =>
        {
            await _directories.InsertAsync(directory, autoSave: true);
            await _files.InsertAsync(deleted, autoSave: true);
            await _files.InsertAsync(active, autoSave: true);
        });
        await WithUnitOfWorkAsync(() => _files.DeleteAsync(deleted.Id, autoSave: true));

        await WithUnitOfWorkAsync(() => _files.ClearDirectoryFromDeletedFilesAsync(directory.Id));

        (await WithUnitOfWorkAsync(() => _files.GetAsync(active.Id))).DirectoryId.ShouldBe(directory.Id);
        using (_dataFilter.Disable<ISoftDelete>())
        {
            (await WithUnitOfWorkAsync(() => _files.GetAsync(deleted.Id))).DirectoryId.ShouldBeNull();
        }

        await WithUnitOfWorkAsync(async () =>
        {
            (await _directories.GetMaxOrderAsync(ownerId, SiteFileContainerNames.Default, null)).ShouldBe(0);
            (await _directories.NameExistsAsync(ownerId, SiteFileContainerNames.Default, "dir", null)).ShouldBeTrue();
            (await _directories.GetAllByUserAsync(ownerId, SiteFileContainerNames.Default)).ShouldHaveSingleItem();
        });
    }

    private static FileDescriptor CreateFile(string name, string? hash, string? referBlobName = null, string containerName = SiteFileContainerNames.Default)
    {
        return new FileDescriptor(
            Guid.NewGuid(), containerName, Guid.NewGuid().ToString("N"), name, "application/pdf", 1,
            hash, referBlobName, directoryId: null, tenantId: null);
    }
}
