using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dignite.Site.Admin.Fields;

public class CreateFieldDto
{
    [Required]
    [StringLength(64)]
    public string Name { get; set; } = default!;

    [Required]
    [StringLength(128)]
    public string DisplayName { get; set; } = default!;

    [Required]
    [StringLength(64)]
    public string FieldTypeName { get; set; } = default!;

    [StringLength(256)]
    public string? Description { get; set; }

    public IDictionary<string, object?>? Configuration { get; set; }

    public string? GroupName { get; set; }
}
