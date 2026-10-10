using System;
using System.IO;
using System.Threading.Tasks;
using Dignite.Site.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.BlobStoring;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Dignite.Site.Files;

/// <summary>
/// The file library end to end against the filesystem container the test module configures: bytes through
/// <c>IFileStorer</c>, rows through <see cref="FileDescriptorManager"/>, tenant-scoped by the same
/// <see cref="ICurrentTenant"/> every other Site entity uses.
/// </summary>
public class FileDescriptorManager_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly FileDescriptorManager _fileDescriptorManager;
    private readonly IFileDescriptorRepository _fileDescriptorRepository;
    private readonly IBlobContainerFactory _blobContainerFactory;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    public FileDescriptorManager_Tests()
    {
        _fileDescriptorManager = GetRequiredService<FileDescriptorManager>();
        _fileDescriptorRepository = GetRequiredService<IFileDescriptorRepository>();
        _blobContainerFactory = GetRequiredService<IBlobContainerFactory>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    [Fact]
    public async Task Should_Store_And_Read_Back_A_Real_File_Tenant_Scoped()
    {
        var bytes = TestFiles.Pdf("tenant scoped");
        var tenantId = Guid.NewGuid();

        FileDescriptor descriptor;
        using (_currentTenant.Change(tenantId))
        {
            descriptor = await UploadAsync(SiteFileContainerNames.Default, "notes.pdf", bytes);

            descriptor.TenantId.ShouldBe(tenantId);
            descriptor.Name.ShouldBe("notes.pdf");
            descriptor.Size.ShouldBe(bytes.Length);
            descriptor.MimeType.ShouldBe("application/pdf");
            descriptor.Hash.Length.ShouldBe(64);

            (await ReadAsync(descriptor)).ShouldBe(bytes);
            (await WithUnitOfWorkAsync(() => _fileDescriptorManager.FindAsync(SiteFileContainerNames.Default, descriptor.BlobName)))
                .ShouldNotBeNull();
        }

        // Another tenant does not see the row, however it addresses it.
        using (_currentTenant.Change(Guid.NewGuid()))
        {
            (await WithUnitOfWorkAsync(() => _fileDescriptorManager.FindAsync(SiteFileContainerNames.Default, descriptor.BlobName)))
                .ShouldBeNull();
        }
    }

    [Fact]
    public async Task Should_Not_Find_A_File_Outside_Sites_Containers()
    {
        (await WithUnitOfWorkAsync(() => _fileDescriptorManager.FindAsync("invoices", "anything"))).ShouldBeNull();

        await Should.ThrowAsync<FileContainerNotAvailableException>(() =>
            UploadAsync("invoices", "notes.pdf", TestFiles.Pdf()));
    }

    /// <summary>
    /// The same bytes twice keep one blob: the second descriptor refers to the first one's, and the copy it
    /// was stored as is deleted again.
    /// </summary>
    [Fact]
    public async Task Should_Dedup_Identical_Content_Into_A_Reference()
    {
        var bytes = TestFiles.Pdf("dedup " + Guid.NewGuid());

        var owner = await UploadAsync(SiteFileContainerNames.Default, "first.pdf", bytes);
        var reference = await UploadAsync(SiteFileContainerNames.Default, "second.pdf", bytes);

        owner.IsReference().ShouldBeFalse();
        reference.IsReference().ShouldBeTrue();
        reference.ReferBlobName.ShouldBe(owner.BlobName);
        reference.Hash.ShouldBeEmpty();
        reference.Name.ShouldBe("second.pdf");

        (await ReadAsync(reference)).ShouldBe(bytes);
        (await BlobExistsAsync(reference.BlobName)).ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_Should_Keep_A_Blob_While_Another_Descriptor_Uses_It()
    {
        var bytes = TestFiles.Pdf("shared " + Guid.NewGuid());
        var owner = await UploadAsync(SiteFileContainerNames.Default, "first.pdf", bytes);
        var reference = await UploadAsync(SiteFileContainerNames.Default, "second.pdf", bytes);

        // The owner goes first: its blob stays for the reference.
        await WithUnitOfWorkAsync(() => _fileDescriptorManager.DeleteAsync(owner));
        (await BlobExistsAsync(owner.BlobName)).ShouldBeTrue();
        (await ReadAsync(reference)).ShouldBe(bytes);

        // The last user goes: the blob goes with it.
        await WithUnitOfWorkAsync(() => _fileDescriptorManager.DeleteAsync(reference));
        (await BlobExistsAsync(owner.BlobName)).ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_Of_A_Reference_Should_Keep_The_Owners_Blob()
    {
        var bytes = TestFiles.Pdf("owned " + Guid.NewGuid());
        var owner = await UploadAsync(SiteFileContainerNames.Default, "first.pdf", bytes);
        var reference = await UploadAsync(SiteFileContainerNames.Default, "second.pdf", bytes);

        await WithUnitOfWorkAsync(() => _fileDescriptorManager.DeleteAsync(reference));

        (await ReadAsync(owner)).ShouldBe(bytes);
    }

    /// <summary>
    /// A deleted owner keeps its hash for the audit trail; the unique hash index covers live rows only, so
    /// the same bytes can be uploaded again afterwards - they get a blob of their own.
    /// </summary>
    [Fact]
    public async Task Should_Accept_The_Same_Bytes_Again_After_They_Were_Deleted()
    {
        var bytes = TestFiles.Pdf("again " + Guid.NewGuid());
        var first = await UploadAsync(SiteFileContainerNames.Default, "first.pdf", bytes);
        await WithUnitOfWorkAsync(() => _fileDescriptorManager.DeleteAsync(first));

        var second = await UploadAsync(SiteFileContainerNames.Default, "again.pdf", bytes);

        second.IsReference().ShouldBeFalse();
        second.Hash.ShouldBe(first.Hash);
        (await ReadAsync(second)).ShouldBe(bytes);

        using (_dataFilter.Disable<ISoftDelete>())
        {
            var deleted = await WithUnitOfWorkAsync(() => _fileDescriptorRepository.GetAsync(first.Id));
            deleted.IsDeleted.ShouldBeTrue();
            deleted.Hash.ShouldBe(first.Hash);
        }
    }

    [Theory]
    [InlineData("C:\\Users\\someone\\Desktop\\photo.png", "photo.png")]
    [InlineData("../../photo.png", "photo.png")]
    public async Task Should_Keep_Only_The_File_Name_Of_What_The_Client_Sent(string sent, string stored)
    {
        var descriptor = await UploadAsync(SiteFileContainerNames.Images, sent, TestFiles.Png(8, 8, seed: sent.Length));

        descriptor.Name.ShouldBe(stored);
    }

    [Fact]
    public async Task Should_Shorten_A_Long_Name_And_Keep_Its_Extension()
    {
        var descriptor = await UploadAsync(SiteFileContainerNames.Images, new string('a', 300) + ".png", TestFiles.Png(8, 8, seed: 7));

        descriptor.Name.Length.ShouldBe(FileDescriptorConsts.MaxNameLength);
        descriptor.Name.ShouldEndWith(".png");
    }

    private Task<FileDescriptor> UploadAsync(string containerName, string fileName, byte[] bytes)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(bytes);
            return await _fileDescriptorManager.CreateAsync(containerName, fileName, stream);
        });
    }

    private Task<byte[]> ReadAsync(FileDescriptor descriptor)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            await using var stream = await _fileDescriptorManager.GetStreamOrNullAsync(descriptor);
            stream.ShouldNotBeNull();
            using var memory = new MemoryStream();
            await stream!.CopyToAsync(memory);
            return memory.ToArray();
        });
    }

    private Task<bool> BlobExistsAsync(string blobName)
    {
        return _blobContainerFactory.Create(SiteFileContainerNames.Default).ExistsAsync(blobName);
    }
}
