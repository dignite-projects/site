namespace Dignite.Site.Files;

public static class FileDescriptorConsts
{
    public static int MaxContainerNameLength { get; set; } = 64;

    public static int MaxBlobNameLength { get; set; } = 256;

    public static int MaxNameLength { get; set; } = 128;

    public static int MaxMimeTypeLength { get; set; } = 128;

    /// <summary>
    /// SHA-256 as upper-case hex (<c>StoredFileInfo.Hash</c>) is 64 characters.
    /// </summary>
    public static int MaxHashLength { get; set; } = 64;
}
