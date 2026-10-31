using System;

namespace Dignite.Site.Public.Preview;

/// <summary>How long the two halves of a preview are honored - see <see cref="SiteTemplatePreviewResourceFilter"/>.</summary>
public class SiteTemplatePreviewOptions
{
    /// <summary>
    /// How long the token in a preview link is accepted. Short: the link is opened right after it is issued, and a
    /// link that leaks - a pasted URL, a browser history - should be worthless by the time anyone else finds it.
    /// </summary>
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How long the preview cookie the token is exchanged for lasts: long enough to look around the site with the
    /// version in place, short enough that a forgotten preview ends on its own.
    /// </summary>
    public TimeSpan CookieLifetime { get; set; } = TimeSpan.FromHours(2);
}
