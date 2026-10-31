using System;
using System.Collections.Generic;
using Dignite.Site.Templates;

namespace Dignite.Site.Admin.Templates;

/// <summary>
/// Who uses a template - see <see cref="ISiteTemplateAdminAppService.GetUsageAsync"/>. What the template
/// editor offers as preview addresses, and what tells an author which pages a change reaches before it is
/// published.
/// </summary>
public class SiteTemplateUsageDto
{
    public Guid TemplateId { get; set; }

    public string TemplateName { get; set; } = default!;

    public SiteTemplateKind Kind { get; set; }

    /// <summary>
    /// The routable pages this template takes part in rendering, the home page first and then by display
    /// name: for a page template the pages whose <c>Template</c> is its name; for the <c>layout</c> every
    /// page; for a partial or an asset the pages of every template that refers to it, followed through
    /// partials that include partials.
    /// </summary>
    public List<SiteTemplatePageUsageDto> Pages { get; set; } = new();

    /// <summary>
    /// The templates whose current version (the published one, else the newest) names this one with
    /// <c>include</c> or <c>asset_url</c> - direct references only, one entry per template, by name.
    /// </summary>
    public List<SiteTemplateReferenceDto> ReferencedBy { get; set; } = new();
}
