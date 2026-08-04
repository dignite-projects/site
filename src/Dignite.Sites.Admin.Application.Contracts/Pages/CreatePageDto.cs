using System.ComponentModel.DataAnnotations;

namespace Dignite.Sites.Admin.Pages;

public class CreatePageDto
{
    [Required]
    [StringLength(64)]
    public string Name { get; set; } = default!;

    [Required]
    [StringLength(128)]
    public string DisplayName { get; set; } = default!;

    [Required]
    [StringLength(256)]
    public string Route { get; set; } = default!;

    [StringLength(128)]
    public string? ContentPathPattern { get; set; }

    [StringLength(256)]
    public string? Template { get; set; }

    public bool IsHomePage { get; set; }

    public int Order { get; set; }

    public bool IsActive { get; set; } = true;
}
