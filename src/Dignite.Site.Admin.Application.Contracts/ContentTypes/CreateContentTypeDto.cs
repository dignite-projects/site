using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dignite.Site.ContentTypes;
using Volo.Abp.Validation;

namespace Dignite.Site.Admin.ContentTypes;

public class CreateContentTypeDto
{
    [Required]
    public Guid PageId { get; set; }

    [Required]
    [DynamicStringLength(typeof(ContentTypeConsts), nameof(ContentTypeConsts.MaxNameLength))]
    [RegularExpression(IdentifierName.Pattern)]
    public string Name { get; set; } = default!;

    [Required]
    [DynamicStringLength(typeof(ContentTypeConsts), nameof(ContentTypeConsts.MaxDisplayNameLength))]
    public string DisplayName { get; set; } = default!;

    [DynamicStringLength(typeof(ContentTypeConsts), nameof(ContentTypeConsts.MaxDescriptionLength))]
    public string? Description { get; set; }

    /// <summary>
    /// The field arrangement. <c>null</c> means none; a duplicate <c>FieldId</c> across entries is
    /// rejected by the domain entity, not re-checked here.
    /// </summary>
    public List<ContentTypeFieldDto>? Fields { get; set; }
}
