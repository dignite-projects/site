using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using Dignite.Site.Admin.Files;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Validation;

namespace Dignite.Site.Mcp.Files;

/// <summary>
/// Listing, reading, uploading and changing files in the site's file library. Each tool is a container check
/// followed by one application service call - the service holds the container's authorization, the upload
/// pipeline (size, type detection from the real bytes, image processing) and the update rules, and none of
/// that is repeated here.
/// </summary>
[McpServerToolType]
public class FileTools : ITransientDependency
{
    protected IFileAdminAppService FileAppService { get; }

    protected SiteMcpFileContainerGuard ContainerGuard { get; }

    protected SiteMcpFileUrlBuilder UrlBuilder { get; }

    protected SiteMcpFileOptions Options { get; }

    public FileTools(
        IFileAdminAppService fileAppService,
        SiteMcpFileContainerGuard containerGuard,
        SiteMcpFileUrlBuilder urlBuilder,
        IOptions<SiteMcpFileOptions> options)
    {
        FileAppService = fileAppService;
        ContainerGuard = containerGuard;
        UrlBuilder = urlBuilder;
        Options = options.Value;
    }

    [McpServerTool(Name = "site_list_files", Title = "List files", ReadOnly = true)]
    [Description(
        "Lists files in a file container, newest first. Without the Contents management permission only " +
        "your own files are returned. Every filter is optional.")]
    public virtual async Task<PagedResultDto<FileDescriptorDto>> ListFilesAsync(
        [Description("A container name from site_list_file_containers.")]
        string containerName,
        [Description("Only files directly in this directory, by id from site_list_directories.")]
        Guid? directoryId = null,
        [Description("Free-text filter over the file name.")]
        string? filter = null,
        [Description("How many to skip, for paging.")]
        int skipCount = 0,
        [Description("How many to return, at most 100. Keep this small unless you need more.")]
        int maxResultCount = 20)
    {
        var container = ContainerGuard.GetContainer(containerName);
        var result = await FileAppService.GetListAsync(new GetFilesInput
        {
            ContainerName = container.Name,
            DirectoryId = directoryId,
            Filter = filter,
            SkipCount = Math.Max(skipCount, 0),
            MaxResultCount = Math.Clamp(maxResultCount, 1, 100)
        });

        foreach (var file in result.Items)
        {
            UrlBuilder.WithUrl(file);
        }

        return result;
    }

    [McpServerTool(Name = "site_get_file", Title = "Get a file", ReadOnly = true)]
    [Description("Returns one file's details, including its url.")]
    public virtual async Task<FileDescriptorDto> GetFileAsync(
        [Description("The file's id, from site_list_files or site_upload_file.")]
        Guid id)
    {
        return UrlBuilder.WithUrl(await GetExposedFileAsync(id));
    }

    [McpServerTool(Name = "site_upload_file", Title = "Upload a file")]
    [Description(
        "Uploads a file from base64-encoded bytes and returns it with its url - the address to put in a " +
        "content field. Small files only: site_list_file_containers gives each container's maxUploadSize. " +
        "The container's own rules (allowed types, size, image processing) apply, and the file type is " +
        "detected from the bytes themselves, so the file name's extension must match the content.")]
    public virtual async Task<FileDescriptorDto> UploadFileAsync(
        [Description("A container name from site_list_file_containers.")]
        string containerName,
        [Description("The file name, with its extension, e.g. 'cover.png'.")]
        string fileName,
        [Description("The file's bytes, base64-encoded. A data: URL is also accepted.")]
        string contentBase64,
        [Description("The directory to put it in, by id from site_list_directories. Omit for the top level.")]
        Guid? directoryId = null)
    {
        var container = ContainerGuard.GetContainer(containerName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw Invalid(nameof(fileName), "A file name is required.");
        }

        var configuration = await FileAppService.GetContainerConfigurationAsync(container.Name);
        var bytes = Decode(contentBase64, Options.GetMaxUploadSize(configuration));

        var dto = await FileAppService.CreateAsync(new CreateFileInput
        {
            ContainerName = container.Name,
            DirectoryId = directoryId,
            // The content type here is never used: the upload pipeline detects the real one from the bytes.
            File = new RemoteStreamContent(new MemoryStream(bytes, writable: false), fileName, "application/octet-stream", bytes.LongLength)
        });

        return UrlBuilder.WithUrl(dto);
    }

    [McpServerTool(Name = "site_update_file", Title = "Rename or move a file", Idempotent = true)]
    [Description(
        "Renames a file or moves it to another of your directories. Only what you pass changes; everything " +
        "else is kept. Needs read access to the file as well: a file you cannot read is reported as not " +
        "found, even if you could otherwise change it.")]
    public virtual async Task<FileDescriptorDto> UpdateFileAsync(
        [Description("The file's id.")]
        Guid id,
        [Description("The new file name. Omit to keep it.")]
        string? name = null,
        [Description("Move it into this directory, by id. Omit to leave it where it is.")]
        Guid? directoryId = null,
        [Description("True to move it to the container's top level. Cannot be combined with directoryId.")]
        bool moveToRoot = false)
    {
        if (moveToRoot && directoryId.HasValue)
        {
            throw Invalid(nameof(directoryId), "Pass either directoryId or moveToRoot, not both.");
        }

        await GetExposedFileAsync(id);

        // Set only what was passed: UpdateFileInput tracks whether DirectoryId was assigned, and an
        // unassigned one is left alone rather than cleared.
        var input = new UpdateFileInput();
        if (name != null)
        {
            input.Name = name;
        }

        if (moveToRoot)
        {
            input.DirectoryId = null;
        }
        else if (directoryId.HasValue)
        {
            input.DirectoryId = directoryId;
        }

        return UrlBuilder.WithUrl(await FileAppService.UpdateAsync(id, input));
    }

    [McpServerTool(Name = "site_delete_file", Title = "Delete a file", Destructive = true)]
    [Description(
        "Deletes a file. Anything that links to its url stops working. Needs read access to the file as " +
        "well: a file you cannot read is reported as not found, even if you could otherwise delete it.")]
    public virtual async Task<string> DeleteFileAsync(
        [Description("The file's id.")]
        Guid id)
    {
        var file = await GetExposedFileAsync(id);
        await FileAppService.DeleteAsync(id);
        return $"Deleted '{file.Name}' ({id}).";
    }

    /// <summary>
    /// The file, if it exists, the caller may read it and it lives in an exposed container - otherwise the
    /// same not-found for all three, so a caller cannot probe which ids exist. Update and delete go through
    /// here too: the container is only known by reading the file, and it must be known to keep MCP inside the
    /// exposed ones. The tool descriptions say so.
    /// </summary>
    protected virtual async Task<FileDescriptorDto> GetExposedFileAsync(Guid id)
    {
        FileDescriptorDto file;
        try
        {
            file = await FileAppService.GetAsync(id);
        }
        catch (Exception exception) when (exception is AbpAuthorizationException or EntityNotFoundException)
        {
            throw ContainerGuard.NotFound("file", id);
        }

        ContainerGuard.EnsureExposed(file.ContainerName, "file", id);
        return file;
    }

    /// <summary>
    /// Decodes the upload, refusing an oversized one from its encoded length alone - before allocating the
    /// decoded copy, so the limit binds ahead of the buffer it protects.
    /// </summary>
    protected virtual byte[] Decode(string contentBase64, long maxUploadSize)
    {
        if (string.IsNullOrWhiteSpace(contentBase64))
        {
            throw Invalid(nameof(contentBase64), "The file content is empty.");
        }

        var payload = contentBase64.Trim();
        if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var marker = payload.IndexOf(";base64,", StringComparison.OrdinalIgnoreCase);
            if (marker < 0)
            {
                throw Invalid(nameof(contentBase64), "A data: URL must be base64-encoded (';base64,').");
            }

            payload = payload[(marker + ";base64,".Length)..];
        }

        // Base64 encodes every 3 bytes as 4 characters, so the encoded length bounds the decoded size - once
        // the whitespace a decoder skips (line-wrapped base64) and the trailing '=' padding are left out.
        if (DecodedLength(payload) > maxUploadSize)
        {
            throw Invalid(nameof(contentBase64), $"The file is larger than {maxUploadSize} bytes, the most this container accepts here.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            throw Invalid(nameof(contentBase64), "The content is not valid base64.");
        }

        if (bytes.Length == 0)
        {
            throw Invalid(nameof(contentBase64), "The file content is empty.");
        }

        if (bytes.LongLength > maxUploadSize)
        {
            throw Invalid(nameof(contentBase64), $"The file is larger than {maxUploadSize} bytes, the most this container accepts here.");
        }

        return bytes;
    }

    /// <summary>The number of bytes <paramref name="base64"/> decodes to, if it is valid base64.</summary>
    protected static long DecodedLength(string base64)
    {
        long significant = 0;
        var padding = 0;
        foreach (var character in base64)
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            significant++;
            padding = character == '=' ? padding + 1 : 0;
        }

        return significant / 4 * 3 - Math.Min(padding, 2);
    }

    protected static AbpValidationException Invalid(string member, string message)
    {
        return new AbpValidationException(message, new List<ValidationResult> { new(message, new[] { member }) });
    }
}
