using System;
using Volo.Abp;

namespace Dignite.Site.Fields;

/// <summary>
/// A platform preset field (currently: the SEO field, <c>Dignite.Site.Seo.SeoFieldNames.FieldName</c>) was
/// seeded by name, not by a literal shared id (see that class's remarks), so deletion is the one operation
/// that would silently disable a platform-recognized semantic for every content type that pulled the field
/// in - <c>FieldManager.DeleteAsync</c> already strips the stored value out of every content's bag before
/// removing the definition, so the loss is not just "unhooked", it is unrecoverable.
/// <para>
/// Leaving a preset field unused (never pulled into a content type) already achieves what a tenant wanting
/// to delete it is usually after, with none of that risk - so this refusal costs a tenant nothing real.
/// </para>
/// </summary>
public class FieldIsPlatformPresetCannotBeDeletedException : BusinessException
{
    public FieldIsPlatformPresetCannotBeDeletedException(Guid fieldId, string name)
        : base(SiteErrorCodes.FieldIsPlatformPresetCannotBeDeleted)
    {
        WithData("FieldId", fieldId);
        WithData("Name", name);
    }
}
