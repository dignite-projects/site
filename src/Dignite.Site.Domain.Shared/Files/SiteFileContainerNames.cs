namespace Dignite.Site.Files;

/// <summary>
/// Blob container names Dignite.FileExplorer is configured against (GitHub issue #41). A
/// <c>FileExplorer</c> field (GitHub issue #42) points its <c>FileExplorerConfiguration.FileContainerName</c>
/// at one of these.
/// <para>
/// Containers are split by <b>policy</b> (allowed types, size limit), not by what a file is used for -
/// purpose-level grouping is what directories are for. Each container's policy is owned by
/// <c>SiteAdminApplicationModule</c>; a host only chooses the storage provider.
/// </para>
/// </summary>
public static class SiteFileContainerNames
{
    /// <summary>
    /// General-purpose, publicly readable attachments - documents plus the image types, so fields that
    /// pointed here before <see cref="Images"/> existed keep working.
    /// </summary>
    public const string Default = "site-files";

    /// <summary>
    /// Publicly readable pictures only (raster formats, no SVG), with a tighter size limit - what an
    /// image-holding FileExplorer field should point at.
    /// </summary>
    public const string Images = "site-images";
}
