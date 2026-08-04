using System.ComponentModel.DataAnnotations;

namespace Dignite.Sites.Admin.Fields;

public class RenameFieldDto
{
    [Required]
    [StringLength(64)]
    public string NewName { get; set; } = default!;
}
