using System;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Timing;

namespace Dignite.Sites.Timing;

/// <summary>
/// The knob a test turns to move time. Resolve it, call <see cref="Set"/>, and every <see cref="IClock"/>
/// in the application follows.
/// <para>
/// Exists for one assertion above all: that a sitemap's <c>lastmod</c> values are identical across two
/// generation runs at two different "now"s. That property is the entire reason <c>lastmod</c> is derived
/// from stored columns, and it cannot be tested against a clock that only moves forwards on its own.
/// </para>
/// <para>
/// Separate from <see cref="TestClockSource"/>, the <see cref="IClock"/> that reads it, because ABP's
/// <see cref="Clock"/> is <see cref="ITransientDependency"/> and a subclass inherits that - a transient
/// clock would hand the test one instance and the code under test another, and setting the time on one
/// would do nothing to the other. Holding the state in a singleton of its own sidesteps that entirely,
/// with no lifetime override and no captive dependency.
/// </para>
/// <para>
/// Unset by default, so every test that does not touch it sees real time and behaves exactly as it did
/// before this existed. A test application is built per test method, so a pinned time cannot leak.
/// </para>
/// </summary>
public class TestClock : ISingletonDependency
{
    /// <summary>The pinned time, or null while the clock is running normally.</summary>
    public DateTime? PinnedNow { get; private set; }

    public void Set(DateTime now)
    {
        PinnedNow = now;
    }

    public void Reset()
    {
        PinnedNow = null;
    }
}
