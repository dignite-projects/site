using System;
using System.ComponentModel.DataAnnotations;
using Dignite.Site.Directories;
using Dignite.Site.Files;
using Volo.Abp.Validation;

namespace Dignite.Site.Admin.Directories;

public class CreateDirectoryInput
{
    [Required]
    [DynamicStringLength(typeof(FileDescriptorConsts), nameof(FileDescriptorConsts.MaxContainerNameLength))]
    public string ContainerName { get; set; } = default!;

    [Required]
    [DynamicStringLength(typeof(DirectoryDescriptorConsts), nameof(DirectoryDescriptorConsts.MaxNameLength))]
    public string Name { get; set; } = default!;

    /// <summary>One of the caller's own directories in the same container, or <c>null</c> for the top level.</summary>
    public Guid? ParentId { get; set; }
}
