using System;
using Shouldly;
using Xunit;

namespace Dignite.Site.Pages;

public class RoutePlaceholderDateFormat_Tests
{
    [Fact]
    public void Should_Return_Whole_Year_For_Year_Only_Format()
    {
        RoutePlaceholderDateFormat.TryGetRange("yyyy", "2026", out var start, out var endExclusive).ShouldBeTrue();

        start.ShouldBe(new DateTime(2026, 1, 1));
        endExclusive.ShouldBe(new DateTime(2027, 1, 1));
    }

    [Fact]
    public void Should_Return_Whole_Month_For_Year_Month_Format()
    {
        RoutePlaceholderDateFormat.TryGetRange("yyyy-MM", "2026-08", out var start, out var endExclusive).ShouldBeTrue();

        start.ShouldBe(new DateTime(2026, 8, 1));
        endExclusive.ShouldBe(new DateTime(2026, 9, 1));
    }

    [Fact]
    public void Should_Roll_Month_Range_Into_The_Next_Year()
    {
        RoutePlaceholderDateFormat.TryGetRange("yyyy-MM", "2026-12", out var start, out var endExclusive).ShouldBeTrue();

        start.ShouldBe(new DateTime(2026, 12, 1));
        endExclusive.ShouldBe(new DateTime(2027, 1, 1));
    }

    [Fact]
    public void Should_Return_Whole_Day_For_Year_Month_Day_Format()
    {
        RoutePlaceholderDateFormat.TryGetRange("yyyy-MM-dd", "2026-08-15", out var start, out var endExclusive).ShouldBeTrue();

        start.ShouldBe(new DateTime(2026, 8, 15));
        endExclusive.ShouldBe(new DateTime(2026, 8, 16));
    }

    [Fact]
    public void Should_Return_Whole_Hour_For_Format_Down_To_The_Hour()
    {
        RoutePlaceholderDateFormat.TryGetRange("yyyy-MM-dd-HH", "2026-08-15-09", out var start, out var endExclusive).ShouldBeTrue();

        start.ShouldBe(new DateTime(2026, 8, 15, 9, 0, 0));
        endExclusive.ShouldBe(new DateTime(2026, 8, 15, 10, 0, 0));
    }

    [Fact]
    public void Should_Fail_When_The_Captured_Value_Does_Not_Match_The_Format()
    {
        RoutePlaceholderDateFormat.TryGetRange("yyyy-MM", "not-a-date", out _, out _).ShouldBeFalse();
    }

    [Fact]
    public void Should_Fail_When_The_Format_Names_No_Recognized_Specifier()
    {
        // A pure literal - nothing here could ever denote a period, whatever the captured text is.
        RoutePlaceholderDateFormat.TryGetRange("'archive'", "archive", out _, out _).ShouldBeFalse();
    }

    /// <summary>
    /// A quoted literal 'M' must not be mistaken for the month specifier - only the free-standing "yyyy"
    /// outside the quotes should count, so this resolves to year granularity, not month.
    /// </summary>
    [Fact]
    public void Should_Not_Treat_A_Quoted_Letter_As_A_Specifier()
    {
        RoutePlaceholderDateFormat.TryGetRange("'M'yyyy", "M2026", out var start, out var endExclusive).ShouldBeTrue();

        start.ShouldBe(new DateTime(2026, 1, 1));
        endExclusive.ShouldBe(new DateTime(2027, 1, 1));
    }
}
