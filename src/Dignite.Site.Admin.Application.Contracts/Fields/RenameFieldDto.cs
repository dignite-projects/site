using System.ComponentModel.DataAnnotations;
using Dignite.Abp.FlexFields;
using Volo.Abp.Validation;

namespace Dignite.Site.Admin.Fields;

public class RenameFieldDto
{
    [Required]
    [DynamicStringLength(typeof(FlexFieldConsts), nameof(FlexFieldConsts.MaxNameLength))]
    [RegularExpression(IdentifierName.Pattern)]
    public string NewName { get; set; } = default!;
}
