using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Contents;

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

    /// <summary>
    /// This content's own address - relative, culture-prefixed (e.g. <c>/blog/my-trip</c>), suitable for
    /// an <c>&lt;a href&gt;</c> with no assumption about scheme/host, correct behind a reverse proxy. An
    /// absolute equivalent is <c>HeadMetadataDto</c>'s job. Populated only by producers that resolved this
    /// content's owning page alongside it (currently: the Public surface); left at <see cref="string.Empty"/>
    /// - never <see langword="null"/> - by others, so a consumer that forgets to check which producer it
    /// called can still safely call string members on it instead of risking a NullReferenceException.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    public IDictionary<string, object?> FieldValues { get; set; } = new Dictionary<string, object?>();
}
