using System;
using Volo.Abp.Timing;

// Deliberately its own namespace, not Dignite.Site.Public.Seo (which Dignite.Site.Public.Application's own
// Seo/ files - e.g. FeedDocumentAppService - already occupy): C# resolves an extension method against the
// caller's own namespace before it ever looks at a using directive, so a same-name-as-caller namespace here
// would silently steal every "Clock.ToOffset(...)" call already written in that project against
// Dignite.Site.Seo.SeoClock, with no compiler warning - confirmed by an isolated repro, not a guess.
namespace Dignite.Site.Public.Application.Contracts.Seo;

/// <summary>
/// The Public-surface sibling of <c>Dignite.Site.Seo.SeoClock</c> (Domain) - same reasoning, smaller
/// surface. Lives here, not there, because <c>Dignite.Site.Public.Web</c> (where <c>SiteDocumentController</c>
/// needs this for its <c>Last-Modified</c> header) cannot reference <c>Dignite.Site.Domain</c>, but can
/// reference this Contracts project.
/// <para>
/// <b>Why this is not just <c>DateTime.SpecifyKind(value, DateTimeKind.Utc)</c>.</b> ABP's <see cref="IClock"/>
/// defaults to <see cref="DateTimeKind.Unspecified"/>, which means stored times are local server times - so
/// hard-coding UTC would label a local time as <c>+00:00</c> and shift it by the server's offset. Routing
/// through <see cref="IClock.Normalize"/> keeps the declared offset consistent with what the clock actually
/// produces.
/// </para>
/// </summary>
public static class ClockExtensions
{
    public static DateTimeOffset ToOffset(this IClock clock, DateTime value)
    {
        return new DateTimeOffset(clock.Normalize(value));
    }
}
