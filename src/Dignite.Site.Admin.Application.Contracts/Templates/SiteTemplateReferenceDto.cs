using System;
using Dignite.Site.Templates;

namespace Dignite.Site.Admin.Templates;

/// <summary>A template that names another one - see <see cref="SiteTemplateUsageDto.ReferencedBy"/>.</summary>
public class SiteTemplateReferenceDto
{
    public Guid TemplateId { get; set; }

    public string TemplateName { get; set; } = default!;

    public SiteTemplateKind Kind { get; set; }

    /// <summary>The version the reference was found in: the published one, else the newest.</summary>
    public int VersionNumber { get; set; }

    /// <summary>Whether <see cref="VersionNumber"/> is the live version; false for a template that was never published.</summary>
    public bool IsPublished { get; set; }

    /// <summary>The function the reference is made with: <c>include</c> or <c>asset_url</c>.</summary>
    public string FunctionName { get; set; } = default!;
}
