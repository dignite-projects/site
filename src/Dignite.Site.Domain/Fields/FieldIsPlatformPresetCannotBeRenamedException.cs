using System;
using Volo.Abp;

namespace Dignite.Site.Fields;

/// <summary>
/// A platform preset field's <c>Name</c> is the only thing that makes it "well-known" across tenants (see
/// <c>Dignite.Site.Seo.SeoFieldNames</c>'s remarks) - each tenant's row has an ordinary, randomly
/// generated <c>Id</c>, so recognition has nothing else stable to key on. Renaming it away would leave
/// nothing pointing at the row until the next reseed pass runs, silently disabling the platform semantic in
/// the meantime.
/// <para>
/// Only the technical <c>Name</c> is protected. <c>DisplayName</c>, <c>Description</c>,
/// <c>FieldTypeName</c>, <c>Configuration</c> and <c>GroupId</c> stay fully editable through
/// <c>FieldManager.UpdateAsync</c>.
/// </para>
/// </summary>
public class FieldIsPlatformPresetCannotBeRenamedException : BusinessException
{
    public FieldIsPlatformPresetCannotBeRenamedException(Guid fieldId, string name)
        : base(SiteErrorCodes.FieldIsPlatformPresetCannotBeRenamed)
    {
        WithData("FieldId", fieldId);
        WithData("Name", name);
    }
}
