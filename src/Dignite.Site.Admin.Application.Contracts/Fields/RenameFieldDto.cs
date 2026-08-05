using System.ComponentModel.DataAnnotations;

namespace Dignite.Site.Admin.Fields;

public class RenameFieldDto
{
    [Required]
    [StringLength(64)]
    public string NewName { get; set; } = default!;
}
