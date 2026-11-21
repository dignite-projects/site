using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.BlobManagementMigration;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Public.Files;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.BlobStoring;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Xunit;
using BlobManagementFile = Dignite.Abp.BlobManagement.FileDescriptor;

namespace Dignite.Site.Files;

/// <summary>
/// The migration of the legacy file library to Blob Management (<see cref="SiteBlobManagementMigrator"/>): ids, addresses and bytes
/// survive, references get bytes of their own, nothing is written in a dry run, and a second run changes nothing.
/// </summary>
public class SiteBlobManagementMigrator_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly SiteBlobManagementMigrator _migrator;
    private readonly LegacySiteFiles _legacyFiles;
    private readonly IFileDescriptorRepository _legacyRepository;
    private readonly IDirectoryDescriptorRepository _directories;
    private readonly IRepository<BlobManagementFile, Guid> _managedFiles;
    private readonly IFilePublicAppService _filePublicAppService;
    private readonly IBlobContainerFactory _containers;

    public SiteBlobManagementMigrator_Tests()
    {
        _migrator = GetRequiredService<SiteBlobManagementMigrator>();
        _legacyFiles = GetRequiredService<LegacySiteFiles>();
        _legacyRepository = GetRequiredService<IFileDescriptorRepository>();
        _directories = GetRequiredService<IDirectoryDescriptorRepository>();
        _managedFiles = GetRequiredService<IRepository<BlobManagementFile, Guid>>();
        _filePublicAppService = GetRequiredService<IFilePublicAppService>();
        _containers = GetRequiredService<IBlobContainerFactory>();
    }

    [Fact]
    public async Task A_Dry_Run_Reports_And_Writes_Nothing()
    {
        await WithUnitOfWorkAsync(() => _legacyFiles.CreateAsync(SiteFileContainerNames.Attachments, "a.pdf", TestFiles.Pdf("a")));

        var report = await WithUnitOfWorkAsync(() => _migrator.MigrateAsync());

        report.DryRun.ShouldBeTrue();
        report.Files.ShouldBe(1);
        report.Owners.ShouldBe(1);
        (await WithUnitOfWorkAsync(() => _managedFiles.GetCountAsync())).ShouldBe(0);
    }

    [Fact]
    public async Task A_File_Keeps_Its_Id_Its_Address_And_Its_Directory()
    {
        Migrate();
        var bytes = TestFiles.Pdf("kept");
        var (file, directoryId) = await WithUnitOfWorkAsync(async () =>
        {
            var parent = await _directories.InsertAsync(new DirectoryDescriptor(Guid.NewGuid(), SiteFileContainerNames.Attachments, "events", null, 0, null), autoSave: true);
            var child = await _directories.InsertAsync(new DirectoryDescriptor(Guid.NewGuid(), SiteFileContainerNames.Attachments, "2026", parent.Id, 0, null), autoSave: true);
            return (await _legacyFiles.CreateAsync(SiteFileContainerNames.Attachments, "kept.pdf", bytes, child.Id), child.Id);
        });

        var report = await WithUnitOfWorkAsync(() => _migrator.MigrateAsync());

        report.Owners.ShouldBe(1);
        report.DirectoryPaths[SiteFileContainerNames.Attachments].ShouldBe(new[] { "events", "events/2026" });
        var migrated = await WithUnitOfWorkAsync(() => _managedFiles.GetAsync(file.Id));
        migrated.BlobName.ShouldBe(file.BlobName);
        migrated.ContainerName.ShouldBe(file.ContainerName);
        migrated.Name.ShouldBe("kept.pdf");
        migrated.MimeType.ShouldBe("application/pdf");
        migrated.Size.ShouldBe(bytes.Length);
        migrated.CreationTime.ShouldBe(file.CreationTime);
        migrated.IsPublic.ShouldBeTrue();
        migrated.GetProperty<string>(SiteBlobManagementMigrator.DirectoryPathProperty).ShouldBe("events/2026");

        // Blob Management now answers for the address, with the bytes that were there.
        await WithUnitOfWorkAsync(() => _legacyRepository.DeleteAsync(file.Id));
        using var content = await _filePublicAppService.GetAsync(file.ContainerName, file.BlobName, new GetFileInput());
        (await ReadAllAsync(content.GetStream())).ShouldBe(bytes);
    }

    [Fact]
    public async Task A_Reference_Gets_Its_Bytes_At_Its_Own_Address_And_An_Orphaned_Blob_Goes()
    {
        Migrate();
        var bytes = TestFiles.Pdf("shared");
        var (owner, reference) = await WithUnitOfWorkAsync(async () =>
        {
            var o = await _legacyFiles.CreateAsync(SiteFileContainerNames.Attachments, "owner.pdf", bytes);
            var r = await _legacyFiles.CreateReferenceAsync(o, "copy.pdf");
            return (o, r);
        });
        // The owner was deleted; its blob stayed for the reference.
        await WithUnitOfWorkAsync(() => _legacyRepository.DeleteAsync(owner.Id));

        var report = await WithUnitOfWorkAsync(() => _migrator.MigrateAsync());

        report.Files.ShouldBe(1);
        report.References.ShouldBe(1);
        report.CopiedBytes.ShouldBe(bytes.Length);
        report.OrphanedBlobs.ShouldBe(1);

        var migrated = await WithUnitOfWorkAsync(() => _managedFiles.GetAsync(reference.Id));
        migrated.BlobName.ShouldBe(reference.BlobName);
        migrated.IsPublic.ShouldBeTrue();

        var container = _containers.Create(SiteFileContainerNames.Attachments);
        (await container.ExistsAsync(reference.BlobName)).ShouldBeTrue();
        (await container.ExistsAsync(owner.BlobName)).ShouldBeFalse();

        using var content = await _filePublicAppService.GetAsync(reference.ContainerName, reference.BlobName, new GetFileInput());
        (await ReadAllAsync(content.GetStream())).ShouldBe(bytes);
    }

    [Fact]
    public async Task Of_Two_Files_With_The_Same_Bytes_Each_Keeps_Bytes_Of_Its_Own()
    {
        Migrate();
        var (owner, reference) = await WithUnitOfWorkAsync(async () =>
        {
            var o = await _legacyFiles.CreateAsync(SiteFileContainerNames.Attachments, "owner.pdf", TestFiles.Pdf("same"));
            return (o, await _legacyFiles.CreateReferenceAsync(o, "copy.pdf"));
        });

        var report = await WithUnitOfWorkAsync(() => _migrator.MigrateAsync());

        // Blob Management shares no bytes between files: the owner keeps its blob, the reference gets a copy.
        report.Owners.ShouldBe(1);
        report.References.ShouldBe(1);
        report.OrphanedBlobs.ShouldBe(0);
        (await WithUnitOfWorkAsync(() => _managedFiles.GetCountAsync())).ShouldBe(2);
        (await _containers.Create(SiteFileContainerNames.Attachments).ExistsAsync(owner.BlobName)).ShouldBeTrue();
        (await _containers.Create(SiteFileContainerNames.Attachments).ExistsAsync(reference.BlobName)).ShouldBeTrue();
    }

    [Fact]
    public async Task A_Second_Run_Changes_Nothing()
    {
        Migrate();
        await WithUnitOfWorkAsync(() => _legacyFiles.CreateAsync(SiteFileContainerNames.Images, "pic.png", TestFiles.Png(16, 16)));

        await WithUnitOfWorkAsync(() => _migrator.MigrateAsync());
        var second = await WithUnitOfWorkAsync(() => _migrator.MigrateAsync());

        second.AlreadyMigrated.ShouldBe(1);
        second.Owners.ShouldBe(0);
        (await WithUnitOfWorkAsync(() => _managedFiles.GetCountAsync())).ShouldBe(1);
    }

    [Fact]
    public async Task Reports_Empty_Directories()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            await _directories.InsertAsync(new DirectoryDescriptor(Guid.NewGuid(), SiteFileContainerNames.Images, "empty", null, 0, null), autoSave: true);
        });

        var report = await WithUnitOfWorkAsync(() => _migrator.MigrateAsync());

        report.Directories.ShouldBe(1);
        report.EmptyDirectories.ShouldBe(1);
        report.DirectoryPaths[SiteFileContainerNames.Images].ShouldBe(new[] { "empty" });
    }

    private void Migrate()
    {
        GetRequiredService<IOptions<SiteBlobManagementMigrationOptions>>().Value.DryRun = false;
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        return memory.ToArray();
    }
}
