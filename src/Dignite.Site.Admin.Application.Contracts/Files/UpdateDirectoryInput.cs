using System.ComponentModel.DataAnnotations;
using Dignite.Site.Files;
using Volo.Abp.Validation;

namespace Dignite.Site.Admin.Files;

public class UpdateDirectoryInput
{
    [Required]
    [DynamicStringLength(typeof(DirectoryDescriptorConsts), nameof(DirectoryDescriptorConsts.MaxNameLength))]
    public string Name { get; set; } = default!;
}
