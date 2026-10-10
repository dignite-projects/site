using System;
using System.ComponentModel.DataAnnotations;
using Dignite.Site.Files;
using Volo.Abp.Content;
using Volo.Abp.Validation;

namespace Dignite.Site.Admin.Files;

/// <summary>
/// An upload: <c>multipart/form-data</c> with the file under <c>file</c>, the rest as query parameters.
/// There is no MIME type here on purpose - the stored type is detected from the bytes.
/// </summary>
public class CreateFileInput
{
    /// <summary>One of Site's file containers (<c>site-images</c>, <c>site-files</c>).</summary>
    [Required]
    [DynamicStringLength(typeof(FileDescriptorConsts), nameof(FileDescriptorConsts.MaxContainerNameLength))]
    public string ContainerName { get; set; } = default!;

    /// <summary>One of the caller's own directories in that container, or <c>null</c> for the root.</summary>
    public Guid? DirectoryId { get; set; }

    [Required]
    public IRemoteStreamContent File { get; set; } = default!;
}
