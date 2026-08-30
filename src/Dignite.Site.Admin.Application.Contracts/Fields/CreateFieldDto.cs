using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dignite.Abp.FlexFields;
using Dignite.Site.Fields;
using Volo.Abp.Validation;

namespace Dignite.Site.Admin.Fields;

public class CreateFieldDto
{
    [Required]
    [DynamicStringLength(typeof(FlexFieldConsts), nameof(FlexFieldConsts.MaxNameLength))]
    [RegularExpression(IdentifierName.Pattern)]
    public string Name { get; set; } = default!;

    [Required]
    [DynamicStringLength(typeof(FlexFieldConsts), nameof(FlexFieldConsts.MaxDisplayNameLength))]
    public string DisplayName { get; set; } = default!;

    [Required]
    [DynamicStringLength(typeof(FlexFieldConsts), nameof(FlexFieldConsts.MaxFieldTypeNameLength))]
    public string FieldTypeName { get; set; } = default!;

    public string? Description { get; set; }

    public IDictionary<string, object?>? Configuration { get; set; }

    [DynamicStringLength(typeof(FieldConsts), nameof(FieldConsts.MaxGroupNameLength))]
    public string? GroupName { get; set; }
}
