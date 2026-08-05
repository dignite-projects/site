using System;
using Volo.Abp.Timing;

namespace Dignite.Sites.Seo;

/// <summary>
/// The one place a stored <see cref="DateTime"/> becomes something with a time zone offset on it.
/// <para>
/// <b>Why this is not just <c>DateTime.SpecifyKind(value, DateTimeKind.Utc)</c>.</b> Sitemap
/// <c>lastmod</c> and feed dates are emitted with an offset - <c>X.Web.Sitemap</c> formats with
/// <c>"yyyy-MM-ddTHH:mm:sszzz"</c>, and syndication uses <see cref="DateTimeOffset"/>. On a
/// <see cref="DateTimeKind.Unspecified"/> value, <c>zzz</c> emits the <i>server's local</i> offset. ABP's
/// <see cref="IClock"/> defaults to <see cref="DateTimeKind.Unspecified"/>, which means stored times are
/// local server times - so hard-coding UTC would label a local time as <c>+00:00</c> and shift every
/// date by the server's offset. Routing everything through <see cref="IClock.Normalize"/> keeps the
/// declared offset consistent with what the clock actually produces, and a host that sets
/// <c>AbpClockOptions.Kind = Utc</c> then gets <c>+00:00</c> for free.
/// </para>
/// </summary>
public static class SeoClock
{
    /// <summary>
    /// The value to hand to a formatter that renders its own offset, e.g. <c>X.Web.Sitemap</c>'s
    /// <c>Url.TimeStamp</c>.
    /// </summary>
    public static DateTime ToSitemapTimeStamp(this IClock clock, DateTime value)
    {
        return clock.Normalize(value);
    }

    /// <summary>The value to hand to <c>SyndicationFeed</c>/<c>JsonFeed</c>, which take offsets directly.</summary>
    public static DateTimeOffset ToOffset(this IClock clock, DateTime value)
    {
        return new DateTimeOffset(clock.Normalize(value));
    }
}
