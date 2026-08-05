using System;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;

namespace Dignite.Site.Timing;

/// <summary>
/// ABP's <see cref="Clock"/> with one change: it reports <see cref="TestClock.PinnedNow"/> when a test has
/// pinned it. Everything else - the configured <see cref="DateTimeKind"/>, normalization, timezone
/// handling - is inherited untouched, so a pinned test still sees exactly the clock semantics production
/// has.
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IClock))]
public class TestClockSource : Clock
{
    protected TestClock TestClock { get; }

    public TestClockSource(
        IOptions<AbpClockOptions> options,
        ICurrentTimezoneProvider currentTimezoneProvider,
        ITimezoneProvider timezoneProvider,
        TestClock testClock)
        : base(options, currentTimezoneProvider, timezoneProvider)
    {
        TestClock = testClock;
    }

    // Normalized, so a pinned value carries whatever kind the application is configured for - the same
    // treatment base.Now's value gets.
    public override DateTime Now =>
        TestClock.PinnedNow.HasValue ? Normalize(TestClock.PinnedNow.Value) : base.Now;
}
