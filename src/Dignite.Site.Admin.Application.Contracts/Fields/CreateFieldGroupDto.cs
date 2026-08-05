using System.ComponentModel.DataAnnotations;

namespace Dignite.Site.Admin.Fields;

public class CreateFieldGroupDto
{
    [Required]
    [StringLength(64)]
    public string Name { get; set; } = default!;

    public int Order { get; set; }
}
