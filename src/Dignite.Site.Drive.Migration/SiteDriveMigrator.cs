using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Files;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using DriveFile = Dignite.Drive.FileDescriptor;
using DriveFileConsts = Dignite.Drive.FileDescriptorConsts;
using LegacyFile = Dignite.Site.Files.FileDescriptor;

namespace Dignite.Site.DriveMigration;

/// <summary>
/// Moves the live files of the file library Site kept before 3.0 (<c>SiteFileDescriptors</c>) into the Drive, in the
/// current tenant, so that nothing a site stored stops working:
/// <list type="bullet">
/// <item>Each live legacy file becomes a Drive file of the same <b>id</b> (what a file field stores), name, type, size,
/// creator and creation time, in the container of the same name.</item>
/// <item>Its physical <b>BLOB name is the legacy file's own BLOB name</b>, so Site's address of it
/// (<c>/api/site-public/files/{container}/{blob}</c>), which reads the Drive by that name, keeps working; no stored
/// address is rewritten. A file that owned its bytes keeps them where they are - the Drive's container of that name
/// reads the same storage. A dedup <i>reference</i> - an address of its own, the bytes of another file's blob - gets a
/// copy of those bytes under its own name; a blob that only references used, its owner deleted, is removed once the
/// copies are committed.</item>
/// <item>The directory a file was filed in is kept on the Drive file as <c>ExtraProperties["Site:DirectoryPath"]</c>
/// (<c>a/b</c>, by name): the open Drive has no directories, Drive Pro can file it again from there.</item>
/// <item>One file per content holds the dedup key; the others share nothing, as if stored without dedup.</item>
/// </list>
/// Bytes are copied with the storage provider directly, past the container's upload pipeline: a legacy file is stored
/// as it was, not converted again. Runs again safely: a file the Drive has (by id) is skipped, a copy that exists is not
/// made twice. <see cref="SiteDriveMigrationOptions.DryRun"/> reports without writing.
/// </summary>
public class SiteDriveMigrator : ITransientDependency
{
    public const string DirectoryPathProperty = "Site:DirectoryPath";

    protected IFileDescriptorRepository LegacyFiles { get; }

    protected IDirectoryDescriptorRepository LegacyDirectories { get; }

    protected IRepository<DriveFile, Guid> DriveFiles { get; }

    protected IBlobProviderSelector BlobProviderSelector { get; }

    protected IBlobContainerConfigurationProvider BlobContainerConfigurationProvider { get; }

    protected IBlobNormalizeNamingService BlobNormalizeNamingService { get; }

    protected IDataFilter DataFilter { get; }

    protected ICurrentTenant CurrentTenant { get; }

    protected IUnitOfWorkManager UnitOfWorkManager { get; }

    protected SiteDriveMigrationOptions Options { get; }

    public ILogger<SiteDriveMigrator> Logger { get; set; } = NullLogger<SiteDriveMigrator>.Instance;

    public SiteDriveMigrator(
        IFileDescriptorRepository legacyFiles,
        IDirectoryDescriptorRepository legacyDirectories,
        IRepository<DriveFile, Guid> driveFiles,
        IBlobProviderSelector blobProviderSelector,
        IBlobContainerConfigurationProvider blobContainerConfigurationProvider,
        IBlobNormalizeNamingService blobNormalizeNamingService,
        IDataFilter dataFilter,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        IOptions<SiteDriveMigrationOptions> options)
    {
        LegacyFiles = legacyFiles;
        LegacyDirectories = legacyDirectories;
        DriveFiles = driveFiles;
        BlobProviderSelector = blobProviderSelector;
        BlobContainerConfigurationProvider = blobContainerConfigurationProvider;
        BlobNormalizeNamingService = blobNormalizeNamingService;
        DataFilter = dataFilter;
        CurrentTenant = currentTenant;
        UnitOfWorkManager = unitOfWorkManager;
        Options = options.Value;
    }

    public virtual async Task<SiteDriveMigrationReport> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var report = new SiteDriveMigrationReport { TenantId = CurrentTenant.Id, DryRun = Options.DryRun };

        var files = await LegacyFiles.GetListAsync(cancellationToken: cancellationToken);
        var directories = await LegacyDirectories.GetListAsync(cancellationToken: cancellationToken);
        if (files.Count == 0 && directories.Count == 0)
        {
            return report;
        }

        report.Files = files.Count;
        var paths = DescribeDirectories(directories, files, report);

        // Every legacy descriptor, deleted ones too: a reference's owner may have been deleted, and its hash is the
        // reference's hash.
        List<LegacyFile> everyFile;
        using (DataFilter.Disable<ISoftDelete>())
        {
            everyFile = await LegacyFiles.GetListAsync(cancellationToken: cancellationToken);
        }

        var owners = everyFile
            .Where(file => !file.IsReference())
            .GroupBy(file => (file.ContainerName, file.BlobName))
            .ToDictionary(group => group.Key, group => group.OrderBy(file => file.IsDeleted).First());

        var claimedDedupKeys = new HashSet<string>();
        foreach (var file in files)
        {
            await MigrateFileAsync(file, owners, paths, claimedDedupKeys, report, cancellationToken);
        }

        await RemoveOrphanedBlobsAsync(files, report, cancellationToken);

        Log(report);
        return report;
    }

    protected virtual async Task MigrateFileAsync(
        LegacyFile file,
        Dictionary<(string ContainerName, string BlobName), LegacyFile> owners,
        Dictionary<Guid, string> paths,
        HashSet<string> claimedDedupKeys,
        SiteDriveMigrationReport report,
        CancellationToken cancellationToken)
    {
        using (DataFilter.Disable<ISoftDelete>())
        {
            if (await DriveFiles.FindAsync(file.Id, includeDetails: false, cancellationToken) != null)
            {
                report.AlreadyMigrated++;
                return;
            }
        }

        if (file.BlobName.Length > DriveFileConsts.MaxBlobNameLength)
        {
            report.Skipped.Add($"{file.ContainerName}/{file.BlobName}: the name is longer than the Drive's {DriveFileConsts.MaxBlobNameLength} characters.");
            return;
        }

        var storedBlobName = file.GetActualBlobName();
        var hash = !file.Hash.IsNullOrEmpty()
            ? file.Hash
            : owners.TryGetValue((file.ContainerName, storedBlobName), out var owner) && !owner.Hash.IsNullOrEmpty()
                ? owner.Hash
                : await ComputeHashAsync(file.ContainerName, storedBlobName, cancellationToken);
        if (hash == null)
        {
            report.Skipped.Add($"{file.ContainerName}/{file.BlobName}: its bytes ({storedBlobName}) are missing from storage.");
            return;
        }

        if (file.IsReference())
        {
            report.References++;
            report.CopiedBytes += file.Size;
            if (!Options.DryRun &&
                !await CopyBlobAsync(file.ContainerName, storedBlobName, file.BlobName, cancellationToken))
            {
                report.References--;
                report.CopiedBytes -= file.Size;
                report.Skipped.Add($"{file.ContainerName}/{file.BlobName}: its bytes ({storedBlobName}) are missing from storage.");
                return;
            }
        }
        else
        {
            report.Owners++;
        }

        var dedupKey = DriveFile.CreateDedupKey(file.TenantId, file.ContainerName, hash);
        var ownsBlob = !claimedDedupKeys.Contains(dedupKey) &&
                       !await DriveFiles.AnyAsync(driveFile => driveFile.DedupKey == dedupKey, cancellationToken);
        if (ownsBlob)
        {
            claimedDedupKeys.Add(dedupKey);
        }

        var migrated = new DriveFile(
            file.Id,
            file.ContainerName,
            file.Name,
            file.BlobName,
            file.MimeType.IsNullOrWhiteSpace() ? "application/octet-stream" : file.MimeType,
            file.Size,
            hash,
            ownsBlob,
            clientId: null,
            tenantId: file.TenantId)
        {
            CreatorId = file.CreatorId,
            CreationTime = file.CreationTime
        };

        if (file.DirectoryId is { } directoryId && paths.TryGetValue(directoryId, out var path))
        {
            migrated.SetProperty(DirectoryPathProperty, path, validate: false);
        }

        if (!Options.DryRun)
        {
            await DriveFiles.InsertAsync(migrated, autoSave: true, cancellationToken);
        }
    }

    /// <summary>
    /// The path of every legacy directory by name (<c>a/b</c>), recorded in the report by container - empty ones
    /// included - and returned by id.
    /// </summary>
    protected virtual Dictionary<Guid, string> DescribeDirectories(
        List<DirectoryDescriptor> directories,
        List<LegacyFile> files,
        SiteDriveMigrationReport report)
    {
        var byId = directories.ToDictionary(directory => directory.Id);
        var paths = new Dictionary<Guid, string>();
        foreach (var directory in directories)
        {
            var names = new List<string>();
            var seen = new HashSet<Guid>();
            for (DirectoryDescriptor? current = directory;
                 current != null && seen.Add(current.Id);
                 current = current.ParentId is { } parentId && byId.TryGetValue(parentId, out var parent) ? parent : null)
            {
                names.Insert(0, current.Name);
            }

            paths[directory.Id] = string.Join('/', names);
        }

        report.Directories = directories.Count;
        report.EmptyDirectories = directories.Count(directory =>
            files.All(file => file.DirectoryId != directory.Id) &&
            directories.All(child => child.ParentId != directory.Id));
        foreach (var directory in directories.OrderBy(directory => paths[directory.Id], StringComparer.Ordinal))
        {
            if (!report.DirectoryPaths.TryGetValue(directory.ContainerName, out var list))
            {
                report.DirectoryPaths[directory.ContainerName] = list = new List<string>();
            }

            list.Add(paths[directory.Id]);
        }

        return paths;
    }

    /// <summary>
    /// Removes, once the migration is committed, the blobs that only references used and whose owner had been
    /// deleted: every live reference to them now has a copy of its own, and no Drive file names them.
    /// </summary>
    protected virtual async Task RemoveOrphanedBlobsAsync(
        List<LegacyFile> files,
        SiteDriveMigrationReport report,
        CancellationToken cancellationToken)
    {
        var orphans = files
            .Where(file => file.IsReference())
            .Select(file => (file.ContainerName, BlobName: file.ReferBlobName))
            .Distinct()
            .Where(blob => !files.Any(file => !file.IsReference() && file.ContainerName == blob.ContainerName && file.BlobName == blob.BlobName))
            .ToList();

        var removable = new List<(string ContainerName, string BlobName)>();
        foreach (var orphan in orphans)
        {
            if (!await DriveFiles.AnyAsync(file => file.ContainerName == orphan.ContainerName && file.BlobName == orphan.BlobName, cancellationToken))
            {
                removable.Add(orphan);
            }
        }

        report.OrphanedBlobs = removable.Count;
        if (Options.DryRun || removable.Count == 0)
        {
            return;
        }

        async Task RemoveAsync()
        {
            foreach (var (containerName, blobName) in removable)
            {
                try
                {
                    var (provider, configuration, name) = GetProvider(containerName, blobName);
                    await provider.DeleteAsync(new BlobProviderDeleteArgs(containerName, configuration, name));
                }
                catch (Exception exception)
                {
                    Logger.LogWarning(exception, "Could not remove the orphaned blob {ContainerName}/{BlobName}; it is unreachable bytes, nothing more.", containerName, blobName);
                }
            }
        }

        if (UnitOfWorkManager.Current != null)
        {
            UnitOfWorkManager.Current.OnCompleted(RemoveAsync);
        }
        else
        {
            await RemoveAsync();
        }
    }

    /// <summary>Copies the stored bytes of <paramref name="sourceBlobName"/> to <paramref name="targetBlobName"/>, unless that exists; <c>false</c> when the source is missing.</summary>
    protected virtual async Task<bool> CopyBlobAsync(string containerName, string sourceBlobName, string targetBlobName, CancellationToken cancellationToken)
    {
        var (provider, configuration, target) = GetProvider(containerName, targetBlobName);
        if (await provider.ExistsAsync(new BlobProviderExistsArgs(containerName, configuration, target, cancellationToken)))
        {
            return true;
        }

        var (_, _, source) = GetProvider(containerName, sourceBlobName);
        await using var stream = await provider.GetOrNullAsync(new BlobProviderGetArgs(containerName, configuration, source, cancellationToken));
        if (stream == null)
        {
            return false;
        }

        await provider.SaveAsync(new BlobProviderSaveArgs(containerName, configuration, target, stream, overrideExisting: false, cancellationToken));
        return true;
    }

    /// <summary>SHA-256 (upper-case hex) of the stored bytes of <paramref name="blobName"/>, or <c>null</c> when they are missing.</summary>
    protected virtual async Task<string?> ComputeHashAsync(string containerName, string blobName, CancellationToken cancellationToken)
    {
        var (provider, configuration, name) = GetProvider(containerName, blobName);
        await using var stream = await provider.GetOrNullAsync(new BlobProviderGetArgs(containerName, configuration, name, cancellationToken));
        if (stream == null)
        {
            return null;
        }

        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    /// <summary>
    /// The container's storage provider, its configuration and the provider's name for <paramref name="blobName"/>:
    /// what <c>IBlobContainer</c> hands a provider, without the container's pipeline.
    /// </summary>
    protected virtual (IBlobProvider Provider, BlobContainerConfiguration Configuration, string BlobName) GetProvider(string containerName, string blobName)
    {
        var configuration = BlobContainerConfigurationProvider.Get(containerName);
        var naming = BlobNormalizeNamingService.NormalizeNaming(configuration, containerName, blobName);
        return (BlobProviderSelector.Get(containerName), configuration, naming.BlobName!);
    }

    protected virtual void Log(SiteDriveMigrationReport report)
    {
        Logger.LogInformation(
            "Site's legacy files {Mode} for tenant {TenantId}: {Files} live files, {AlreadyMigrated} already in the Drive, " +
            "{Owners} owning their bytes, {References} dedup references ({CopiedBytes} bytes copied), {OrphanedBlobs} orphaned blobs removed, " +
            "{Directories} directories ({EmptyDirectories} empty), {Skipped} skipped.",
            report.DryRun ? "(dry run, nothing written)" : "migrated to the Drive",
            report.TenantId?.ToString() ?? "host",
            report.Files, report.AlreadyMigrated, report.Owners, report.References, report.CopiedBytes, report.OrphanedBlobs,
            report.Directories, report.EmptyDirectories, report.Skipped.Count);

        foreach (var (containerName, paths) in report.DirectoryPaths)
        {
            Logger.LogInformation("Legacy directories of {ContainerName}: {Paths}", containerName, string.Join(", ", paths));
        }

        foreach (var skipped in report.Skipped)
        {
            Logger.LogWarning("Not migrated: {Skipped}", skipped);
        }
    }
}
