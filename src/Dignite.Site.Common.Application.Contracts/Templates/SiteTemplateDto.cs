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
}
