using System;
using Dignite.Site.Timing;
using Shouldly;
using Volo.Abp.Timing;
using Xunit;

namespace Dignite.Site.Timing;

/// <summary>
/// The test clock is itself load-bearing - the sitemap's central guarantee is asserted by moving it - so
/// its own wiring is checked rather than assumed. Getting this wrong is silent: a clock that never moves
/// makes "lastmod did not change" pass for the wrong reason.
/// </summary>
public class TestClock_Tests : SiteDomainTestBase<SiteDomainTestModule>
{
    private readonly IClock _clock;
    private readonly TestClock _testClock;

    public TestClock_Tests()
    {
        _clock = GetRequiredService<IClock>();
        _testClock = GetRequiredService<TestClock>();
    }

    [Fact]
    public void Should_Replace_The_Applications_Clock()
    {
        _clock.ShouldBeOfType<TestClockSource>();
    }

    [Fact]
    public void Should_Run_On_Real_Time_Until_Pinned()
    {
        (_clock.Now - DateTime.Now).Duration().ShouldBeLessThan(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Pinning_Should_Be_Visible_To_Everything_Resolving_IClock()
    {
        var pinned = new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        _testClock.Set(pinned);

        _clock.Now.ShouldBe(_clock.Normalize(pinned));
        GetRequiredService<IClock>().Now.ShouldBe(_clock.Normalize(pinned));
    }

    [Fact]
    public void Resetting_Should_Hand_Time_Back()
    {
        _testClock.Set(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _testClock.Reset();

        (_clock.Now - DateTime.Now).Duration().ShouldBeLessThan(TimeSpan.FromMinutes(1));
    }
}
