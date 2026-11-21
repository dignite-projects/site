using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Admin.Files;

/// <summary>One of the file containers of the site (<c>SiteFileContainerNames</c>) and what it takes.</summary>
public class SiteFileContainerDto
{
    public string Name { get; set; } = default!;

    /// <summary>The largest file the container stores, in bytes.</summary>
    public long MaxFileSize { get; set; }

    /// <summary>The extensions the container takes (<c>.png</c>, ...); empty when it takes any.</summary>
    public string[] AllowedFileExtensions { get; set; } = Array.Empty<string>();

    /// <summary>A picture is resized into this width, when the container resizes; <c>null</c> when it does not.</summary>
    public int? ImageMaxWidth { get; set; }

    /// <summary>A picture is resized into this height, when the container resizes; <c>null</c> when it does not.</summary>
    public int? ImageMaxHeight { get; set; }
}

public class GetSiteFilesInput : PagedAndSortedResultRequestDto
{
    [Required]
    public string ContainerName { get; set; } = default!;

    /// <summary>A part of the name, whatever its case.</summary>
    public string? Filter { get; set; }

    /// <summary>
    /// The directory whose files are listed; <c>null</c> is the top of the container - or the whole container, when it
    /// has no directories. Directories are Blob Management's (<c>api/blob-management/directories</c>).
    /// </summary>
    public Guid? DirectoryId { get; set; }
}

public class CreateSiteFileUploadTicketInput
{
    [Required]
    public string ContainerName { get; set; } = default!;

    /// <summary>
    /// The directory the file is filed in; <c>null</c> is the top of the container. The container has to allow
    /// directories and the directory has to be one of its own, which Blob Management checks.
    /// </summary>
    public Guid? DirectoryId { get; set; }
}

public class SiteFileUploadTicketDto
{
    /// <summary>The id the file will have: what <c>ConfirmAsync</c> takes.</summary>
    public Guid FileId { get; set; }

    /// <summary>
    /// Where the browser posts the file (the multipart field <c>file</c>, or the raw body with <c>?name=</c>), relative
    /// to the host that serves Blob Management - the same host as the administration API.
    /// </summary>
    public string UploadUrl { get; set; } = default!;

    public DateTime ExpirationTime { get; set; }
}

public class SiteFileDto
{
    public Guid Id { get; set; }

    public string ContainerName { get; set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>Detected from the bytes: a JPEG a container stores as WebP is <c>image/webp</c>.</summary>
    public string MimeType { get; set; } = default!;

    /// <summary>The size of what is stored, in bytes.</summary>
    public long Size { get; set; }

    /// <summary>
    /// Site's address of the file (<c>/api/site-public/files/{container}/{blob}</c>, relative): what a file field or
    /// an SEO image stores. <c>null</c> while the file is pending.
    /// </summary>
    public string? Url { get; set; }

    public bool IsPending { get; set; }

    public Guid? CreatorId { get; set; }

    public DateTime CreationTime { get; set; }
}
