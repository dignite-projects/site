using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dignite.Sites.Contents;

namespace Dignite.Sites.Admin.Contents;

public class CreateContentDto
{
    [Required]
    public Guid ContentTypeId { get; set; }

    [Required]
    [StringLength(32)]
    public string CultureName { get; set; } = default!;

    /// <summary>Empty is legitimate: the single content of a page (总体设计 §2.4).</summary>
    [StringLength(256)]
    public string Slug { get; set; } = string.Empty;

    [Required]
    public DateTime PublishTime { get; set; }

    public ContentStatus Status { get; set; } = ContentStatus.Draft;

    /// <summary>
    /// Keyed by field <c>Name</c>. Keys the content type does not declare are dropped, and every value is
    /// validated against its field type - both by <c>ContentManager</c>, not repeated here.
    /// </summary>
    public IDictionary<string, object?>? FieldValues { get; set; }
}
