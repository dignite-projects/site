using System;
using Volo.Abp;

namespace Dignite.Site.Contents;

/// <summary>
/// A content was given a page and a content type that do not agree (总体设计 §2.5: <c>Content.PageId</c>
/// must equal its content type's <c>PageId</c>).
/// <para>
/// Worth rejecting loudly rather than reconciling silently, because the two ids are stored separately
/// and are read by different paths: routing reads <c>PageId</c>, rendering reads <c>ContentTypeId</c>.
/// A mismatch produces a content that is reachable at a URL whose page knows nothing about its shape.
/// </para>
/// </summary>
public class ContentPageInconsistentException : BusinessException
{
    public ContentPageInconsistentException(Guid contentTypeId)
        : base(SiteErrorCodes.ContentPageInconsistent)
    {
        WithData("ContentTypeId", contentTypeId);
    }
}
