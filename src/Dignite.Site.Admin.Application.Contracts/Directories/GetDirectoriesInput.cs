using System.ComponentModel.DataAnnotations;

namespace Dignite.Site.Admin.Directories;

public class GetDirectoriesInput
{
    [Required]
    public string ContainerName { get; set; } = default!;
}
