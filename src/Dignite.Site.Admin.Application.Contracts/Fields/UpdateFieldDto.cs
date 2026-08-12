using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dignite.Abp.FlexFields;
using Dignite.Site.Fields;
using Volo.Abp.Validation;

namespace Dignite.Site.Admin.Fields;

/// <summary>No <c>Name</c> - renaming goes through <see cref="RenameFieldDto"/> instead (总体设计 §2.4).</summary>
public class UpdateFieldDto
{
    [Required]
    [DynamicStringLength(typeof(FlexFieldConsts), nameof(FlexFieldConsts.MaxDisplayNameLength))]
    public string DisplayName { get; set; } = default!;

    [Required]
    [DynamicStringLength(typeof(FlexFieldConsts), nameof(FlexFieldConsts.MaxFieldTypeNameLength))]
    public string FieldTypeName { get; set; } = default!;

    [DynamicStringLength(typeof(FlexFieldConsts), nameof(FlexFieldConsts.MaxDescriptionLength))]
    public string? Description { get; set; }

    public IDictionary<string, object?>? Configuration { get; set; }

    [DynamicStringLength(typeof(FieldConsts), nameof(FieldConsts.MaxGroupNameLength))]
    public string? GroupName { get; set; }
}
