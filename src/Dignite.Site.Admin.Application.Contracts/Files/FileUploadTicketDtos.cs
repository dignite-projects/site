using System;
using System.ComponentModel.DataAnnotations;
using Dignite.Site.Files;
using Volo.Abp.Validation;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// What to ask for an upload link: where the file goes and what it is called. The bytes follow later, straight from
/// wherever the file is to the link, so they never have to pass through whoever asked.
/// </summary>
public class CreateFileUploadTicketInput
{
    /// <summary>One of Site's file containers.</summary>
    [Required]
    [DynamicStringLength(typeof(FileDescriptorConsts), nameof(FileDescriptorConsts.MaxContainerNameLength))]
    public string ContainerName { get; set; } = default!;

    /// <summary>The file's name, with its extension. The stored type is detected from the bytes, so the extension has to match.</summary>
    [Required]
    [DynamicStringLength(typeof(FileDescriptorConsts), nameof(FileDescriptorConsts.MaxNameLength))]
    public string FileName { get; set; } = default!;

    /// <summary>One of the caller's own directories in that container, or <c>null</c> for the root.</summary>
    public Guid? DirectoryId { get; set; }
}

/// <summary>An upload link, as a path of the admin API: the host in front of it is the caller's to put.</summary>
public class FileUploadTicketDto
{
    /// <summary>
    /// <c>api/site-admin/files/upload/{ticket}</c>: a <c>PUT</c> to it with the file's bytes as the body stores the file.
    /// It needs no other credential, so it is as secret as the token it carries.
    /// </summary>
    public string Path { get; set; } = default!;

    /// <summary>After this the link is refused. It is also refused after its first use.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>The largest body the link accepts, in bytes: the container's limit.</summary>
    public long MaxBytes { get; set; }

    public string ContainerName { get; set; } = default!;

    public string FileName { get; set; } = default!;
}
