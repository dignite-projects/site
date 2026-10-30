using System;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Templates;

/// <summary>One immutable saved state of a template's source.</summary>
public class SiteTemplateVersionDto : CreationAuditedEntityDto<Guid>
{
    public Guid TemplateId { get; set; }

    public int Number { get; set; }

    public string Body { get; set; } = default!;

    public string? Comment { get; set; }
}
