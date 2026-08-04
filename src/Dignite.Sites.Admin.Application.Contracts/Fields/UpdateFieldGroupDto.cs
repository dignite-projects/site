using System.ComponentModel.DataAnnotations;

namespace Dignite.Sites.Admin.Fields;

public class UpdateFieldGroupDto
{
    [Required]
    [StringLength(64)]
    public string Name { get; set; } = default!;

    public int Order { get; set; }
}
