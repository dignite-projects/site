using System;
using Volo.Abp;

namespace Dignite.Site.Contents;

/// <summary>
/// A content was given <see cref="ContentStatus.Draft"/> together with a <c>PublishTime</c> that has not
/// arrived yet (总体设计 §2.4).
/// <para>
/// A draft is not publicly reachable no matter what <c>PublishTime</c> says, so a future date on one is
/// not a schedule anything would ever act on - it would just sit there. Scheduling for the future is
/// <see cref="ContentStatus.Published"/> with a future <c>PublishTime</c> instead; the "published, not
/// yet due" query filter (<c>Content.IsPublished</c>) is what keeps that hidden until the time arrives,
/// so nothing further has to run.
/// </para>
/// </summary>
public class ContentDraftCannotHaveFuturePublishTimeException : BusinessException
{
    public ContentDraftCannotHaveFuturePublishTimeException(DateTime publishTime)
        : base(SiteErrorCodes.ContentDraftCannotHaveFuturePublishTime)
    {
        WithData("PublishTime", publishTime);
    }
}
