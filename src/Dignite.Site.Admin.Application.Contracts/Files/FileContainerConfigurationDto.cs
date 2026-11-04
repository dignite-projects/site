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

    /// <summary>
    /// The width an image is shrunk to fit, in pixels, read from the container's own configuration; null when the container
    /// does not resize images. An image smaller than this is not enlarged.
    /// </summary>
    public int? ImageMaxWidth { get; set; }

    /// <summary>The height an image is shrunk to fit, in pixels; see <see cref="ImageMaxWidth"/>.</summary>
    public int? ImageMaxHeight { get; set; }

    /// <summary>
    /// Whether a stored image is re-encoded to a smaller file: the bytes kept are not the bytes uploaded, so the size
    /// differs from the file that was sent. False when the container stores images as they are.
    /// </summary>
    public bool CompressesImages { get; set; }

    public string? CreateDirectoryPermissionName { get; set; }

    public string? CreateFilePermissionName { get; set; }

    public string? UpdateFilePermissionName { get; set; }

    public string? DeleteFilePermissionName { get; set; }

    /// <summary>Unset: the container's files are publicly readable.</summary>
    public string? GetFilePermissionName { get; set; }
}
