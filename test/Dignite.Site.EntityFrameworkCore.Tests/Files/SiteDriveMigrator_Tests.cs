using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.DriveMigration;
using Dignite.Site.EntityFrameworkCore;
using Dignite.Site.Public.Files;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.BlobStoring;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Xunit;
using DriveFile = Dignite.Drive.FileDescriptor;

namespace Dignite.Site.Files;

/// <summary>
/// The migration of the legacy file library to the Drive (<see cref="SiteDriveMigrator"/>): ids, addresses and bytes
/// survive, references get bytes of their own, nothing is written in a dry run, and a second run changes nothing.
/// </summary>
public class SiteDriveMigrator_Tests : SiteEntityFrameworkCoreTestBase
{
    private readonly SiteDriveMigrator _migrator;
    private readonly LegacySiteFiles _legacyFiles;
    private readonly IFileDescriptorRepository _legacyRepository;
    private readonly IDirectoryDescriptorRepository _directories;
    private readonly IRepository<DriveFile, Guid> _driveFiles;
    private readonly IFilePublicAppService _filePublicAppService;
    private readonly IBlobContainerFactory _containers;

    public SiteDriveMigrator_Tests()
    {
        _migrator = GetRequiredService<SiteDriveMigrator>();
        _legacyFiles = GetRequiredService<LegacySiteFiles>();
        _legacyRepository = GetRequiredService<IFileDescriptorRepository>();
        _directories = GetRequiredService<IDirectoryDescriptorRepository>();
        _driveFiles = GetRequiredService<IRepository<DriveFile, Guid>>();
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
        (await WithUnitOfWorkAsync(() => _driveFiles.GetCountAsync())).ShouldBe(0);
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
        var migrated = await WithUnitOfWorkAsync(() => _driveFiles.GetAsync(file.Id));
        migrated.BlobName.ShouldBe(file.BlobName);
        migrated.ContainerName.ShouldBe(file.ContainerName);
        migrated.Name.ShouldBe("kept.pdf");
        migrated.MimeType.ShouldBe("application/pdf");
        migrated.Size.ShouldBe(bytes.Length);
        migrated.CreationTime.ShouldBe(file.CreationTime);
        migrated.OwnsBlob().ShouldBeTrue();
        migrated.GetProperty<string>(SiteDriveMigrator.DirectoryPathProperty).ShouldBe("events/2026");

        // The Drive now answers for the address, with the bytes that were there.
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

        var migrated = await WithUnitOfWorkAsync(() => _driveFiles.GetAsync(reference.Id));
        migrated.BlobName.ShouldBe(reference.BlobName);
        migrated.Hash.ShouldBe(owner.Hash);
        migrated.OwnsBlob().ShouldBeTrue();

        var container = _containers.Create(SiteFileContainerNames.Attachments);
        (await container.ExistsAsync(reference.BlobName)).ShouldBeTrue();
        (await container.ExistsAsync(owner.BlobName)).ShouldBeFalse();

        using var content = await _filePublicAppService.GetAsync(reference.ContainerName, reference.BlobName, new GetFileInput());
        (await ReadAllAsync(content.GetStream())).ShouldBe(bytes);
    }

    [Fact]
    public async Task Of_Two_Files_With_The_Same_Bytes_One_Holds_The_Dedup_Key()
    {
        Migrate();
        var (owner, reference) = await WithUnitOfWorkAsync(async () =>
        {
            var o = await _legacyFiles.CreateAsync(SiteFileContainerNames.Attachments, "owner.pdf", TestFiles.Pdf("same"));
            return (o, await _legacyFiles.CreateReferenceAsync(o, "copy.pdf"));
        });

        var report = await WithUnitOfWorkAsync(() => _migrator.MigrateAsync());

        report.Owners.ShouldBe(1);
        report.References.ShouldBe(1);
        report.OrphanedBlobs.ShouldBe(0);
        var files = await WithUnitOfWorkAsync(() => _driveFiles.GetListAsync());
        files.Count(file => file.OwnsBlob()).ShouldBe(1);
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
        (await WithUnitOfWorkAsync(() => _driveFiles.GetCountAsync())).ShouldBe(1);
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
        GetRequiredService<IOptions<SiteDriveMigrationOptions>>().Value.DryRun = false;
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        return memory.ToArray();
    }
}
