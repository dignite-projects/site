using System;
using Volo.Abp;

namespace Dignite.Site.ContentTypes;

/// <summary>
/// A content type referenced the same field twice. Both usages would resolve against one bag key, so the
/// value would surface twice with possibly conflicting Required flags and index twice.
/// </summary>
public class ContentTypeFieldDuplicatedException : BusinessException
{
    public ContentTypeFieldDuplicatedException(Guid fieldId)
        : base(SiteErrorCodes.ContentTypeFieldDuplicated)
    {
        WithData("FieldId", fieldId);
    }
}
