using System;
using System.ComponentModel.DataAnnotations;
using Dignite.Site.Pages;
using Volo.Abp.Validation;

namespace Dignite.Site.Admin.Pages;

public class CreatePageDto
{
    [Required]
    [DynamicStringLength(typeof(PageConsts), nameof(PageConsts.MaxNameLength))]
    [RegularExpression(IdentifierName.Pattern)]
    public string Name { get; set; } = default!;

    [Required]
    [DynamicStringLength(typeof(PageConsts), nameof(PageConsts.MaxDisplayNameLength))]
    public string DisplayName { get; set; } = default!;

    [Required]
    [DynamicStringLength(typeof(PageConsts), nameof(PageConsts.MaxRouteLength))]
    [RegularExpression(PageConsts.RoutePattern)]
    public string Route { get; set; } = default!;

    [DynamicStringLength(typeof(PageConsts), nameof(PageConsts.MaxTemplateLength))]
    [RegularExpression(PageConsts.TemplatePattern)]
    public string? Template { get; set; }

    [DynamicStringLength(typeof(PageConsts), nameof(PageConsts.MaxContentTemplateLength))]
    [RegularExpression(PageConsts.TemplatePattern)]
    public string? ContentTemplate { get; set; }

    /// <summary>Null for a top-level page. Purely organizational - see <see cref="Dignite.Site.Pages.Page.ParentId"/>.</summary>
    public Guid? ParentId { get; set; }

    public bool IsActive { get; set; } = true;
}
