using System.ComponentModel;

namespace Dignite.Site.Mcp.ContentTypes;

/// <summary>
/// One field pulled into a content type, addressed by the field's name (总体设计 §2.3, §6.2.4).
/// <para>
/// The name-addressed counterpart of <c>ContentTypeFieldDto</c>, which carries a <c>FieldId</c>. Only the
/// usage half is here: what the field intrinsically is - its type, its configuration - belongs to the
/// definition and is edited with <c>create_field</c> / <c>update_field</c>.
/// </para>
/// </summary>
public class McpContentTypeFieldInput
{
    [Description("The field's machine name from the field library, e.g. 'title'. The field must already exist - create it with create_field first.")]
    public string Field { get; set; } = default!;

    [Description("Whether a value is required when saving content of this type. The same field can be required here and optional in another type.")]
    public bool Required { get; set; }

    [Description("Whether this field's value is indexed for filtering and search in this type.")]
    public bool Searchable { get; set; }

    [Description("Whether list views show this field.")]
    public bool ShowInList { get; set; }

    [Description("Override the field's display name, for this content type only. Omit to use the field's own.")]
    public string? DisplayName { get; set; }

    [Description(
        "Position within this content type. Lower comes first. Entries that share the same value keep " +
        "the order they were listed in this call, so a duplicate value is safe but still worth avoiding.")]
    public int Order { get; set; }
}
