namespace Dignite.FlexFields.Site.Seo;

/// <summary>
/// The one platform-recognized SEO field, seeded into every tenant's field library
/// (总体设计 §5.3, GitHub issue #14).
/// <para>
/// <see cref="FieldName"/> - not a literal shared <c>Guid</c> - is the actual "well-known id" here. Each
/// tenant's row still gets an ordinary, randomly generated <c>Id</c>; the <c>Field</c> table's primary key
/// is a single, tenant-wide-unique column (see the <c>PK_SiteFields</c> migration), so the same literal
/// <c>Guid</c> cannot exist once per tenant in a shared database. <see cref="Name"/> is unique only
/// <i>within</i> a tenant, which is exactly the scope this needs: <c>FieldManager</c> refuses to delete or
/// rename a field with this name (see its <c>DeleteAsync</c>/<c>RenameAsync</c> guards), so once seeded it
/// stays resolvable by every tenant's own lookup, without needing to be the same row identity everywhere.
/// </para>
/// </summary>
public static class SeoFieldNames
{
    /// <summary>The seeded field's technical name - the key its value is stored under in a content's bag.</summary>
    public const string FieldName = "seo";

    /// <summary>Registration name of <c>SeoFieldType</c>, the field type this field is bound to. Named
    /// <c>ControlName</c>, not <c>FieldTypeName</c>, to match every other field type's own
    /// <c>public const string ControlName</c> - grepping for that name should find Seo too, even though
    /// (unlike its siblings) the constant has to live here rather than directly on <c>SeoFieldType</c>,
    /// since seeding code needs it without instantiating the type.</summary>
    public const string ControlName = "Seo";
}
