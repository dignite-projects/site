using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dignite.Sites.Contents;

namespace Dignite.Sites.Admin.Contents;

public class UpdateContentDto
{
    [StringLength(256)]
    public string Slug { get; set; } = string.Empty;

    [Required]
    public DateTime PublishTime { get; set; }

    public ContentStatus Status { get; set; }

    /// <summary><c>null</c> leaves the existing values untouched - a status-only or schedule-only update.</summary>
    public IDictionary<string, object?>? FieldValues { get; set; }

    /// <summary>Moves the content to a different shape within the same page, if set.</summary>
    public Guid? ContentTypeId { get; set; }
}
