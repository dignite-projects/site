namespace Dignite.Site.Admin.Files;

/// <summary>
/// What a client needs to know about a container before uploading to it: its limits, and which
/// permission each operation needs (so a UI can hide what the user may not do).
/// </summary>
public class FileContainerConfigurationDto
{
    /// <summary>The largest file the container accepts, in bytes.</summary>
    public long MaxBlobSize { get; set; }

    /// <summary>Allowed file extensions; empty means the container does not restrict them.</summary>
    public string[] AllowedFileTypeNames { get; set; } = [];

    public string? CreateDirectoryPermissionName { get; set; }

    public string? CreateFilePermissionName { get; set; }

    public string? UpdateFilePermissionName { get; set; }

    public string? DeleteFilePermissionName { get; set; }

    /// <summary>Unset: the container's files are publicly readable.</summary>
    public string? GetFilePermissionName { get; set; }
}
