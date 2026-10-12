using System.ComponentModel.DataAnnotations;

namespace Dignite.Site.Admin.Files;

public class GetDirectoriesInput
{
    [Required]
    public string ContainerName { get; set; } = default!;
}
