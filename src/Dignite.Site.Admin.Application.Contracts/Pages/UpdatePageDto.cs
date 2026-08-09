using System;
using System.ComponentModel.DataAnnotations;

namespace Dignite.Site.Admin.Pages;

public class UpdatePageDto
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

    [StringLength(256)]
    public string? Template { get; set; }

    public bool IsHomePage { get; set; }

    public int Order { get; set; }

    /// <summary>Null for a top-level page. Purely organizational - see <see cref="Dignite.Site.Pages.Page.ParentId"/>.</summary>
    public Guid? ParentId { get; set; }

    public bool IsActive { get; set; } = true;
}
