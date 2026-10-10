using System.ComponentModel.DataAnnotations;
using Dignite.Site.Directories;
using Volo.Abp.Validation;

namespace Dignite.Site.Admin.Directories;

public class UpdateDirectoryInput
{
    [Required]
    [DynamicStringLength(typeof(DirectoryDescriptorConsts), nameof(DirectoryDescriptorConsts.MaxNameLength))]
    public string Name { get; set; } = default!;
}
