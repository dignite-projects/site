using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.ContentTypes;

public class ContentTypeDto : FullAuditedEntityDto<Guid>
{
    public Guid PageId { get; set; }

    public string Name { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>Ordered, matching <c>ContentType.Fields</c>.</summary>
    public List<ContentTypeFieldDto> Fields { get; set; } = new();

    /// <summary>The schema.org type this content type's contents represent, if any (GitHub issue #20).</summary>
    public SchemaOrgType SchemaType { get; set; }
}
