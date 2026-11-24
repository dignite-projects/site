using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dignite.Site.Contents;
using Volo.Abp.Validation;

namespace Dignite.Site.Admin.Contents;

/// <summary>A content as an editor has it so far, for the address it will have once saved.</summary>
public class GetContentPathInput
{
    [Required]
    public Guid PageId { get; set; }

    [Required]
    [DynamicStringLength(typeof(ContentConsts), nameof(ContentConsts.MaxCultureNameLength))]
    public string CultureName { get; set; } = default!;

    /// <summary>Empty for the page's own address.</summary>
    [DynamicStringLength(typeof(ContentConsts), nameof(ContentConsts.MaxSlugLength))]
    [RegularExpression(SlugNormalizer.AllowedCharactersPattern)]
    public string Slug { get; set; } = string.Empty;

    public DateTime PublishTime { get; set; }

    /// <summary>Keyed by field <c>Name</c>, as <see cref="CreateContentDto.FieldValues"/>.</summary>
    public IDictionary<string, object?>? FieldValues { get; set; }
}
