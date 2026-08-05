using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dignite.Site.ContentTypes;

namespace Dignite.Site.Admin.ContentTypes;

public class CreateContentTypeDto
{
    [Required]
    public Guid PageId { get; set; }

    [Required]
    [StringLength(64)]
    public string Name { get; set; } = default!;

    [Required]
    [StringLength(128)]
    public string DisplayName { get; set; } = default!;

    [StringLength(512)]
    public string? Description { get; set; }

    /// <summary>
    /// The field arrangement. <c>null</c> means none; a duplicate <c>FieldId</c> across entries is
    /// rejected by the domain entity, not re-checked here.
    /// </summary>
    public List<ContentTypeFieldDto>? Fields { get; set; }

    /// <summary>The schema.org type this content type's contents represent. Defaults to none.</summary>
    public SchemaOrgType SchemaType { get; set; }
}
