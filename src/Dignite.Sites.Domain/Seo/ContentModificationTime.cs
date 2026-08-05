using System;
using Dignite.Sites.Contents;

namespace Dignite.Sites.Seo;

/// <summary>
/// When a content genuinely last changed - the single definition shared by the sitemap's <c>lastmod</c>
/// and a feed item's dates, so the two can never disagree about the same content.
/// </summary>
public static class ContentModificationTime
{
    /// <summary>
    /// <c>max(LastModificationTime ?? CreationTime, PublishTime)</c>.
    /// <para>
    /// Every term is a stored column, which is the property that matters: run the generator twice and the
    /// answer is identical. Reporting the current time instead is the failure that costs a site the
    /// <c>lastmod</c> signal across its whole sitemap, not just on the rows that were wrong.
    /// </para>
    /// <para>
    /// <see cref="Content.PublishTime"/> is included because a scheduled content's URL comes into
    /// existence when that time arrives - and since it is stored rather than observed, a scheduled publish
    /// that changed nothing else still does not drift the value toward "now".
    /// </para>
    /// </summary>
    public static DateTime Of(Content content)
    {
        var edited = content.LastModificationTime ?? content.CreationTime;

        return edited > content.PublishTime ? edited : content.PublishTime;
    }
}
