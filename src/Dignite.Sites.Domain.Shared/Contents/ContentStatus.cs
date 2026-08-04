namespace Dignite.Sites.Contents;

/// <summary>
/// Publication state of one content row. Together with <c>Content.PublishTime</c> this drives scheduled
/// publishing: a <see cref="Draft"/> whose publish time has passed is flipped to <see cref="Published"/>
/// by a background job.
/// </summary>
public enum ContentStatus : byte
{
    /// <summary>Not publicly readable. Never enters a sitemap, and is force-noindexed when previewed.</summary>
    Draft = 0,

    /// <summary>Publicly readable once <c>PublishTime</c> has passed.</summary>
    Published = 1,

    /// <summary>Withdrawn from publication but retained. Not routable, not in the sitemap.</summary>
    Archived = 2
}
