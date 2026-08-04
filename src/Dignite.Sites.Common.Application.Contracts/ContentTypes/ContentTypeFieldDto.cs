using System;

namespace Dignite.Sites.ContentTypes;

/// <summary>
/// One field pulled into a content type, DTO-shaped: a reference to a field in the library plus how this
/// type uses it. Mirrors the domain value object <c>ContentTypeField</c> property-for-property
/// (总体设计 §2.3) - it carries no definition data (type, configuration) of its own, only the usage.
/// </summary>
public class ContentTypeFieldDto
{
    public Guid FieldId { get; set; }

    public bool Required { get; set; }

    public bool Searchable { get; set; }

    public bool ShowInList { get; set; }

    public string? DisplayName { get; set; }

    public int Order { get; set; }
}
