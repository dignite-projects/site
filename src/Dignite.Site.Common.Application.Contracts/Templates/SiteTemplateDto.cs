using System;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Templates;

/// <summary>
/// A template's header, read-shaped: which version is live and which is the newest. The versions
/// themselves (<see cref="SiteTemplateVersionDto"/>) are fetched one at a time - a template's history
/// grows without bound and a list should not carry it.
/// </summary>
public class SiteTemplateDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; } = default!;

    /// <summary>The live version's number, or null when the template has never been published.</summary>
    public int? PublishedVersionNumber { get; set; }

    /// <summary>
    /// The highest version number. Not on the entity, so the mapper leaves it alone and whichever
    /// application service builds the DTO fills it - one batched query for a list, not one per row.
    /// </summary>
    public int LatestVersionNumber { get; set; }

    /// <summary>What the name says the template is - see <see cref="SiteTemplateNames.GetKind"/>.</summary>
    public SiteTemplateKind Kind { get; set; }

    /// <summary>
    /// How many things use the template: for a page template the pages whose <c>Template</c> is its name,
    /// for a partial or an asset the templates whose current version refers to it with <c>include</c> or
    /// <c>asset_url</c>. Null for the layout, which every page uses, and on a read that did not count
    /// (<c>GetAsync</c>, <c>FindByNameAsync</c>, a create or an update); the list fills it.
    /// </summary>
    public int? UsageCount { get; set; }
}
