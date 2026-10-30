using System.ComponentModel.DataAnnotations;
using Dignite.Site.Templates;
using Volo.Abp.Validation;

namespace Dignite.Site.Admin.Templates;

public class CreateSiteTemplateDto
{
    /// <summary>
    /// The name a <c>Page.Template</c> refers to, or <c>layout</c> for the template that wraps every page.
    /// </summary>
    [Required]
    [DynamicStringLength(typeof(SiteTemplateConsts), nameof(SiteTemplateConsts.MaxNameLength))]
    [RegularExpression(SiteTemplateConsts.NamePattern)]
    public string Name { get; set; } = default!;

    /// <summary>The template source, in Scriban syntax. Empty is allowed; a syntax error is not.</summary>
    [Required(AllowEmptyStrings = true)]
    [DynamicStringLength(typeof(SiteTemplateConsts), nameof(SiteTemplateConsts.MaxBodyLength))]
    public string Body { get; set; } = default!;

    [DynamicStringLength(typeof(SiteTemplateConsts), nameof(SiteTemplateConsts.MaxCommentLength))]
    public string? Comment { get; set; }

    /// <summary>Whether the first version goes live at once. False stages it: saved, but not rendered.</summary>
    public bool Publish { get; set; } = true;
}
