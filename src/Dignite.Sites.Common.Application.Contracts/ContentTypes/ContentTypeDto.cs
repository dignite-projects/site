using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Dignite.Sites.ContentTypes;

public class ContentTypeDto : FullAuditedEntityDto<Guid>
{
    public Guid PageId { get; set; }

    public string Name { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>Ordered, matching <c>ContentType.Fields</c>.</summary>
    public List<ContentTypeFieldDto> Fields { get; set; } = new();
}
