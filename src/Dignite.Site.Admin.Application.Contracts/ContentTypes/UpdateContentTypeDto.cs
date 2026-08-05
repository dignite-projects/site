using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dignite.Site.ContentTypes;

namespace Dignite.Site.Admin.ContentTypes;

/// <summary>No <c>PageId</c> - a content type never moves to another page (总体设计 §2.3).</summary>
public class UpdateContentTypeDto
{
    [Required]
    [StringLength(64)]
    public string Name { get; set; } = default!;

    [Required]
    [StringLength(128)]
    public string DisplayName { get; set; } = default!;

    [StringLength(512)]
    public string? Description { get; set; }

    public List<ContentTypeFieldDto>? Fields { get; set; }
}
