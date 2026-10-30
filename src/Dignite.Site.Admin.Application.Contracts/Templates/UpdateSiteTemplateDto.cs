using System.ComponentModel.DataAnnotations;
using Dignite.Site.Templates;
using Volo.Abp.Validation;

namespace Dignite.Site.Admin.Templates;

/// <summary>
/// Appends a new version to a template. Versions are immutable - there is no way to edit one in place.
/// </summary>
public class UpdateSiteTemplateDto
{
    /// <summary>The template source, in Scriban syntax. Empty is allowed; a syntax error is not.</summary>
    [Required(AllowEmptyStrings = true)]
    [DynamicStringLength(typeof(SiteTemplateConsts), nameof(SiteTemplateConsts.MaxBodyLength))]
    public string Body { get; set; } = default!;

    [DynamicStringLength(typeof(SiteTemplateConsts), nameof(SiteTemplateConsts.MaxCommentLength))]
    public string? Comment { get; set; }

    /// <summary>Whether the new version goes live at once. False stages it: saved, but not rendered.</summary>
    public bool Publish { get; set; } = true;
}
