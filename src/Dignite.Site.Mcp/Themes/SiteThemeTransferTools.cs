using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Admin.Permissions;
using Dignite.Site.Admin.Themes;
using Dignite.Site.Mcp.Errors;
using Dignite.Site.Mcp.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Validation;

namespace Dignite.Site.Mcp.Themes;

/// <summary>
/// The theme as one zip, by link: the client downloads it to disk or sends one from disk with a command, so a theme's
/// files - a stylesheet built where its templates can be read, dozens of templates at once - never pass through the
/// model, which would have to write every byte out and could change some on the way.
/// </summary>
[McpServerToolType]
public class SiteThemeTransferTools : ITransientDependency
{
    public const string MergeMode = "merge";
    public const string ReplaceMode = "replace";

    protected ISiteThemeTransferAdminAppService TransferAppService { get; }
    protected ISiteThemeAdminAppService ThemeAppService { get; }
    protected SiteMcpFileOptions Options { get; }
    protected IHttpContextAccessor? HttpContextAccessor { get; }

    public SiteThemeTransferTools(
        ISiteThemeTransferAdminAppService transferAppService,
        ISiteThemeAdminAppService themeAppService,
        IOptions<SiteMcpFileOptions> options,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        TransferAppService = transferAppService;
        ThemeAppService = themeAppService;
        Options = options.Value;
        HttpContextAccessor = httpContextAccessor;
    }

    [McpServerTool(Name = "site_create_theme_download_url", Title = "Get a link that downloads the theme", ReadOnly = true)]
    [Description(
        "Returns a one-time link, good for ten minutes, that downloads the theme as a zip: each file at its own path " +
        "(templates have no extension), and .site-theme.json with the default layout. Run the command on the machine " +
        "that should have the zip (replace ZIP_PATH) - the bytes go there and never through you: command for sh, bash, " +
        "zsh and cmd.exe, powerShellCommand for PowerShell. Use it to build the theme's stylesheet where its templates " +
        "can be read, or to keep a copy. The working copy by default; give 'release' for a release.")]
    [Authorize(SiteAdminPermissions.Themes.Default)]
    public virtual async Task<McpThemeTransferUrl> CreateDownloadUrlAsync(
        [Description("A release number from site_list_releases. Omit for the working copy.")]
        int? release = null)
    {
        Guid? releaseId = null;
        if (release is { } number)
        {
            var releases = await ThemeAppService.GetReleasesAsync();
            releaseId = releases.Items.FirstOrDefault(r => r.Number == number)?.Id
                        ?? throw new McpEntityNotFoundException(
                            "release",
                            number.ToString(CultureInfo.InvariantCulture),
                            hint: "Call site_list_releases for the releases this site has.");
        }

        var link = await TransferAppService.CreateExportLinkAsync(new CreateSiteThemeExportLinkInput { ReleaseId = releaseId });
        var url = GetBaseUrl().TrimEnd('/') + "/" + link.Path;

        return new McpThemeTransferUrl
        {
            Url = url,
            ExpiresAt = link.ExpiresAt,
            Command = $"curl -sS --fail-with-body -o \"ZIP_PATH\" \"{url}\"",
            PowerShellCommand = $"Invoke-WebRequest -Uri \"{url}\" -OutFile \"ZIP_PATH\""
        };
    }

    [McpServerTool(Name = "site_create_theme_upload_url", Title = "Get a link that imports a theme package")]
    [Description(
        "Returns a one-time link, good for ten minutes, that takes a theme zip as the body of a PUT and brings it into " +
        "the working copy - nothing goes live: check, preview and publish it afterwards as any change. The zip is shaped " +
        "as site_create_theme_download_url makes it: files at their own paths at the top of the zip (not inside a " +
        "folder), templates without an extension, text only. mode 'merge' (the default) adds the zip's files or " +
        "replaces the file at their path and leaves the rest - send a zip holding only css/site.css to update the " +
        "stylesheet; 'replace' makes the working copy exactly the zip, deleting what it does not have. Run the command " +
        "on the machine that has the zip (replace ZIP_PATH): command for sh, bash, zsh and cmd.exe, powerShellCommand " +
        "for PowerShell. Its response lists the files added, modified, deleted and unchanged, and the syntax errors of " +
        "the templates it brought in.")]
    [Authorize(SiteAdminPermissions.Themes.Edit)]
    public virtual async Task<McpThemeTransferUrl> CreateUploadUrlAsync(
        [Description("'merge' (default) or 'replace'.")]
        string mode = MergeMode)
    {
        var replace = mode switch
        {
            MergeMode => false,
            ReplaceMode => true,
            _ => throw new AbpValidationException(
                $"mode is '{mode}'; it can be '{MergeMode}' or '{ReplaceMode}'.",
                new[] { new System.ComponentModel.DataAnnotations.ValidationResult($"mode can be '{MergeMode}' or '{ReplaceMode}'.", new[] { nameof(mode) }) })
        };

        var link = await TransferAppService.CreateImportLinkAsync(new CreateSiteThemeImportLinkInput { Replace = replace });
        var url = GetBaseUrl().TrimEnd('/') + "/" + link.Path;

        return new McpThemeTransferUrl
        {
            Url = url,
            ExpiresAt = link.ExpiresAt,
            Command = $"curl -sS --fail-with-body -X PUT -H \"Content-Type: application/zip\" --data-binary \"@ZIP_PATH\" \"{url}\"",
            PowerShellCommand = $"Invoke-RestMethod -Method Put -ContentType application/zip -InFile \"ZIP_PATH\" -Uri \"{url}\""
        };
    }

    /// <summary>
    /// Where the links point: <see cref="SiteMcpFileOptions.UploadBaseUrl"/>, which a file upload link starts with too,
    /// else the scheme and host this MCP request came in on (see <c>FileTools.GetUploadBaseUrl</c>). A host behind a
    /// gateway routes <c>api/site-admin/theme/download/*</c> and <c>api/site-admin/theme/upload/*</c> through, as it does
    /// <c>api/site-admin/files/upload/*</c>.
    /// </summary>
    protected virtual string GetBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(Options.UploadBaseUrl))
        {
            return Options.UploadBaseUrl!;
        }

        var request = HttpContextAccessor?.HttpContext?.Request
                      ?? throw new InvalidOperationException(
                          $"The address of a theme link is unknown: {nameof(SiteMcpFileOptions)}.{nameof(SiteMcpFileOptions.UploadBaseUrl)} is not set and there is no request to take it from.");

        return $"{request.Scheme}://{request.Host}{request.PathBase}";
    }
}

/// <summary>A theme download or upload link, and the commands that follow it.</summary>
public class McpThemeTransferUrl
{
    public string Url { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }

    /// <summary>For sh, bash, zsh and cmd.exe; replace ZIP_PATH.</summary>
    public string Command { get; set; } = default!;

    /// <summary>For PowerShell; replace ZIP_PATH.</summary>
    public string PowerShellCommand { get; set; } = default!;
}
