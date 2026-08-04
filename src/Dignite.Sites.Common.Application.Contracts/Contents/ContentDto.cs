using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Dignite.Sites.Contents;

/// <summary>
/// One content record, read-shaped: one row, one language (总体设计 §2.4). <c>FieldValues</c> is a plain,
/// JSON-friendly dictionary keyed by field <c>Name</c> - the same shape <c>CreateContentDto</c> and
/// <c>UpdateContentDto</c> accept, so a value read here round-trips straight back through an update
/// without any reshaping. It is never the kernel's <c>FlexFieldDictionary</c>.
/// </summary>
public class ContentDto : FullAuditedEntityDto<Guid>
{
    public Guid PageId { get; set; }

    public Guid ContentTypeId { get; set; }

    public string CultureName { get; set; } = default!;

    public string Slug { get; set; } = default!;

    public DateTime PublishTime { get; set; }

    public ContentStatus Status { get; set; }

    public IDictionary<string, object?> FieldValues { get; set; } = new Dictionary<string, object?>();
}
