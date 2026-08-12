using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dignite.Site.ContentTypes;
using Volo.Abp.Validation;

namespace Dignite.Site.Admin.ContentTypes;

/// <summary>No <c>PageId</c> - a content type never moves to another page (总体设计 §2.3).</summary>
public class UpdateContentTypeDto
{
    [Required]
    [DynamicStringLength(typeof(ContentTypeConsts), nameof(ContentTypeConsts.MaxNameLength))]
    [RegularExpression(IdentifierName.Pattern)]
    public string Name { get; set; } = default!;

    [Required]
    [DynamicStringLength(typeof(ContentTypeConsts), nameof(ContentTypeConsts.MaxDisplayNameLength))]
    public string DisplayName { get; set; } = default!;

    [DynamicStringLength(typeof(ContentTypeConsts), nameof(ContentTypeConsts.MaxDescriptionLength))]
    public string? Description { get; set; }

    public List<ContentTypeFieldDto>? Fields { get; set; }
}
