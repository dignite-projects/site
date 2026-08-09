namespace Dignite.Site.Files;

/// <summary>
/// Blob container names Dignite.FileExplorer is configured against in this solution's hosts
/// (GitHub issue #41). A <c>FileExplorer</c> field (GitHub issue #42) points its
/// <c>FileExplorerConfiguration.FileContainerName</c> at one of these.
/// </summary>
public static class SiteFileContainerNames
{
    /// <summary>The one container configured today - filesystem in dev, production provider still open (#41).</summary>
    public const string Default = "site-files";
}
