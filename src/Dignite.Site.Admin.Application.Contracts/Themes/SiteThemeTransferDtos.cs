using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Content;

namespace Dignite.Site.Admin.Themes;

/// <summary>A theme package to bring into the working copy.</summary>
public class ImportSiteThemeInput
{
    /// <summary>The zip, as <see cref="ISiteThemeTransferAdminAppService.ExportAsync"/> makes it.</summary>
    [Required]
    public IRemoteStreamContent File { get; set; } = default!;

    /// <summary>
    /// False (merge): the package's files are added or replace the file at their path, and the rest of the working copy
    /// stays. True (replace): the working copy becomes the package - a file the package does not have is deleted.
    /// </summary>
    public bool Replace { get; set; }
}

/// <summary>What an import did to the working copy, path by path.</summary>
public class SiteThemeImportResultDto
{
    public List<string> Added { get; set; } = new();

    public List<string> Modified { get; set; } = new();

    public List<string> Deleted { get; set; } = new();

    public List<string> Unchanged { get; set; } = new();

    /// <summary>Whether the default layout was set from the package's manifest.</summary>
    public bool DefaultLayoutSet { get; set; }

    /// <summary>
    /// The syntax errors of the templates that were added or changed, by path - imported all the same, as a save would
    /// keep them; a publish is refused while any remain.
    /// </summary>
    public Dictionary<string, List<SiteThemeSyntaxErrorDto>> SyntaxErrors { get; set; } = new();
}

public class CreateSiteThemeExportLinkInput
{
    /// <summary>The release to export; null for the working copy.</summary>
    public Guid? ReleaseId { get; set; }
}

public class CreateSiteThemeImportLinkInput
{
    /// <inheritdoc cref="ImportSiteThemeInput.Replace"/>
    public bool Replace { get; set; }
}

/// <summary>
/// A link that downloads a theme package, or takes one as the body of a <c>PUT</c>, without a credential of its own: it is
/// the credential - signed, good for ten minutes and one use, and run as the one who asked for it.
/// </summary>
public class SiteThemeTransferLinkDto
{
    /// <summary>The link, relative to the host the administration API is reached at: no leading slash.</summary>
    public string Path { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }
}

/// <summary>Where the links of <see cref="ISiteThemeTransferAdminAppService"/> live, for the service that issues them and the controller that serves them.</summary>
public static class SiteThemeTransferPaths
{
    /// <summary>The route of the download endpoint, below <c>api/site-admin/theme</c>.</summary>
    public const string DownloadRouteTemplate = "download/{ticket}";

    /// <summary>The route of the upload endpoint, below <c>api/site-admin/theme</c>.</summary>
    public const string UploadRouteTemplate = "upload/{ticket}";

    public static string BuildDownload(string protectedTicket) => "api/site-admin/theme/download/" + protectedTicket;

    public static string BuildUpload(string protectedTicket) => "api/site-admin/theme/upload/" + protectedTicket;
}
