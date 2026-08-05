namespace Dignite.Site.Contents;

/// <summary>
/// Publication state of one content row. Together with <c>Content.PublishTime</c> this drives scheduled
/// publishing: scheduling for the future means setting <see cref="Published"/> with a publish time that
/// has not arrived yet, which the read path keeps hidden until then (<c>Content.IsPublished</c>).
/// <see cref="Draft"/> can never carry a future publish time - <c>ContentManager</c> rejects that
/// combination, since nothing would ever act on it.
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
