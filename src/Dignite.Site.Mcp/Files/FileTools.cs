using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Admin.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Mcp.Files;

/// <summary>
/// The site's files, which Blob Management keeps: which containers there are, an upload by link, and the files of a
/// container. The bytes of an upload go from the machine the file is on to Blob Management, by the command a link comes
/// with, and never through the model, which would have to write out every byte and could change some on the way; the
/// model files the upload with <c>site_confirm_file</c>. What each container takes, and who may use it, is
/// <see cref="IFileAdminAppService"/>'s - the tools only call it.
/// </summary>
[McpServerToolType]
public class FileTools : ITransientDependency
{
    protected IFileAdminAppService FileAppService { get; }

    protected SiteMcpOptions Options { get; }

    protected IHttpContextAccessor? HttpContextAccessor { get; }

    public FileTools(
        IFileAdminAppService fileAppService,
        IOptions<SiteMcpOptions> options,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        FileAppService = fileAppService;
        Options = options.Value;
        HttpContextAccessor = httpContextAccessor;
    }

    [McpServerTool(Name = "site_list_file_containers", Title = "List file containers", ReadOnly = true)]
    [Description(
        "Lists the site's file containers you may upload to, with the largest file each takes and its allowed file " +
        "extensions. A container that processes pictures says so: imageMaxWidth and imageMaxHeight are the size a picture " +
        "is shrunk to fit (a smaller one is not enlarged), and a JPEG - or a PNG that is not small - is stored as WebP, " +
        "so the size, the name and the " +
        "mimeType of the stored file differ from the file you sent; use the url, name and mimeType a file tool returns. " +
        "Call this before any other file tool: they take a containerName from this list.")]
    [Authorize]
    public virtual async Task<List<SiteFileContainerDto>> ListFileContainersAsync()
    {
        return (await FileAppService.GetContainersAsync()).Items.ToList();
    }

    [McpServerTool(Name = "site_create_upload_url", Title = "Get a link that uploads a file")]
    [Description(
        "Returns a one-use link, good for a few minutes, that uploads one file to a container. Run the command on the " +
        "machine the file is on (replace FILE_PATH) - the bytes go there and never through you: command for sh, bash, " +
        "zsh and cmd.exe, powerShellCommand for PowerShell. Its output is the pending file as JSON; the file is not " +
        "kept, and has no url, until you call site_confirm_file with its id. Upload one file per link.")]
    [Authorize]
    public virtual async Task<McpUploadUrl> CreateUploadUrlAsync(
        [Description("A name from site_list_file_containers: images, attachments or theme-assets.")]
        string containerName)
    {
        var ticket = await FileAppService.CreateUploadTicketAsync(new CreateSiteFileUploadTicketInput
        {
            ContainerName = containerName
        });

        var url = GetBaseUrl().TrimEnd('/') + "/" + ticket.UploadUrl.TrimStart('/');
        return new McpUploadUrl
        {
            FileId = ticket.FileId,
            Url = url,
            ExpiresAt = DateTime.SpecifyKind(ticket.ExpirationTime, DateTimeKind.Utc),
            ContainerName = containerName,
            Command = $"curl -X POST -F \"file=@FILE_PATH\" \"{url}\"",
            PowerShellCommand = $"curl.exe -X POST -F \"file=@FILE_PATH\" \"{url}\""
        };
    }

    [McpServerTool(Name = "site_confirm_file", Title = "Keep an uploaded file")]
    [Description(
        "Keeps a file uploaded with a link from site_create_upload_url and makes it readable by the site's visitors. " +
        "Returns the file: store its id, name and url in a content field (a file field takes an array of them), or its " +
        "url in an SEO image field or a template. A file you do not confirm is deleted after a day.")]
    [Authorize]
    public virtual Task<SiteFileDto> ConfirmFileAsync(
        [Description("The fileId site_create_upload_url returned.")]
        Guid fileId)
    {
        return FileAppService.ConfirmAsync(fileId);
    }

    [McpServerTool(Name = "site_list_files", Title = "List the files of a container", ReadOnly = true)]
    [Description(
        "Lists the files kept in one container, newest first, a page at a time (up to 100). Files that are uploaded and " +
        "not confirmed yet are not listed.")]
    [Authorize]
    public virtual async Task<McpFilePage> ListFilesAsync(
        [Description("A name from site_list_file_containers.")]
        string containerName,
        [Description("A part of the file's name, whatever its case.")]
        string? filter = null,
        [Description("How many files to skip. Default 0.")]
        int skip = 0,
        [Description("How many files to return, 1 to 100. Default 20.")]
        int take = 20)
    {
        var page = await FileAppService.GetListAsync(new GetSiteFilesInput
        {
            ContainerName = containerName,
            Filter = filter,
            Sorting = "CreationTime desc",
            SkipCount = Math.Max(skip, 0),
            MaxResultCount = Math.Clamp(take, 1, 100)
        });

        return new McpFilePage { TotalCount = page.TotalCount, Items = page.Items.ToList() };
    }

    [McpServerTool(Name = "site_get_file", Title = "Get a file", ReadOnly = true)]
    [Description("Returns one file by its id: its name, type, size and url.")]
    [Authorize]
    public virtual Task<SiteFileDto> GetFileAsync(
        [Description("The file's id, from site_list_files or site_confirm_file.")]
        Guid fileId)
    {
        return FileAppService.GetAsync(fileId);
    }

    [McpServerTool(Name = "site_delete_file", Title = "Delete a file", Destructive = true)]
    [Description(
        "Deletes a file for good: its address stops working wherever a content, a template or a setting still uses it. " +
        "Delete one only when nothing uses it. A file that is not there is not an error.")]
    [Authorize]
    public virtual Task DeleteFileAsync(
        [Description("The file's id.")]
        Guid fileId)
    {
        return FileAppService.DeleteAsync(fileId);
    }

    /// <summary>Where the links point: <see cref="SiteMcpOptions.BaseUrl"/>, else the scheme and host this MCP request came in on.</summary>
    protected virtual string GetBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(Options.BaseUrl))
        {
            return Options.BaseUrl!;
        }

        var request = HttpContextAccessor?.HttpContext?.Request
                      ?? throw new InvalidOperationException(
                          $"The address of an upload link is unknown: {nameof(SiteMcpOptions)}.{nameof(SiteMcpOptions.BaseUrl)} is not set and there is no request to take it from.");
        return $"{request.Scheme}://{request.Host}{request.PathBase}";
    }
}

/// <summary>An upload link as <c>site_create_upload_url</c> hands it over: where to send the file, and how.</summary>
public class McpUploadUrl
{
    /// <summary>The id the file will have; <c>site_confirm_file</c> takes it.</summary>
    public Guid FileId { get; set; }

    /// <summary>Send the file here as a multipart <c>POST</c>, in the field <c>file</c>. The link is the credential: single use, short-lived.</summary>
    public string Url { get; set; } = default!;

    public string Method { get; set; } = "POST";

    public DateTime ExpiresAt { get; set; }

    public string ContainerName { get; set; } = default!;

    /// <summary>
    /// A command that sends the file from sh, bash, zsh or cmd.exe, with <c>FILE_PATH</c> to be replaced by the path of
    /// the file. Run it on the machine the file is on. In PowerShell, where <c>curl</c> is another command, use
    /// <see cref="PowerShellCommand"/>.
    /// </summary>
    public string Command { get; set; } = default!;

    /// <summary><see cref="Command"/> for PowerShell (Windows PowerShell 5.1 and PowerShell 7).</summary>
    public string PowerShellCommand { get; set; } = default!;
}

public class McpFilePage
{
    public long TotalCount { get; set; }

    public List<SiteFileDto> Items { get; set; } = new();
}
