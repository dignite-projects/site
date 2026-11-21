using System;
using System.Collections.Generic;

namespace Dignite.Site.BlobManagementMigration;

/// <summary>What <see cref="SiteBlobManagementMigrator"/> did, or would do in a dry run, for one tenant (or the host).</summary>
public class SiteBlobManagementMigrationReport
{
    public Guid? TenantId { get; set; }

    public bool DryRun { get; set; }

    /// <summary>Live legacy files found.</summary>
    public int Files { get; set; }

    /// <summary>Files Blob Management has already (by id), from an earlier run: skipped.</summary>
    public int AlreadyMigrated { get; set; }

    /// <summary>Files whose bytes were their own blob: the file of Blob Management names that blob, nothing is copied.</summary>
    public int Owners { get; set; }

    /// <summary>
    /// Dedup references: files with an address of their own and the bytes of another file's blob. Their bytes are
    /// copied to their own address, so that address keeps working.
    /// </summary>
    public int References { get; set; }

    /// <summary>The bytes copied for <see cref="References"/>.</summary>
    public long CopiedBytes { get; set; }

    /// <summary>Blobs only references used, whose owner had been deleted: removed once their bytes are copied.</summary>
    public int OrphanedBlobs { get; set; }

    /// <summary>Directories of the legacy library, every one recorded in <see cref="DirectoryPaths"/>.</summary>
    public int Directories { get; set; }

    /// <summary>Of <see cref="Directories"/>, those with no file and no subdirectory.</summary>
    public int EmptyDirectories { get; set; }

    /// <summary>The path of every legacy directory (<c>a/b</c>, by name), by container - empty ones included.</summary>
    public Dictionary<string, List<string>> DirectoryPaths { get; } = new();

    /// <summary>Files not migrated, with the reason (<c>{container}/{blob}: reason</c>).</summary>
    public List<string> Skipped { get; } = new();
}
