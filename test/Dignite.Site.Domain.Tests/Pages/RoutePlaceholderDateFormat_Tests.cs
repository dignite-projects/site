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

    /// <summary>
    /// {publishTime:yyyy}/{publishTime:MM} captured as 2025/08 is one month. A past year on purpose: judged
    /// one part at a time, "08" alone parsed as August of the current year, and whichever part came last
    /// overwrote the other - /2025/08 then filtered on a month that is not in 2025 at all.
    /// </summary>
    [Fact]
    public void Should_Compose_Year_And_Month_Parts_Into_One_Month()
    {
        RoutePlaceholderDateFormat.TryGetRange(new[] { ("yyyy", "2025"), ("MM", "08") }, out var start, out var endExclusive)
            .ShouldBeTrue();

        start.ShouldBe(new DateTime(2025, 8, 1));
        endExclusive.ShouldBe(new DateTime(2025, 9, 1));
    }

    [Fact]
    public void Should_Compose_Parts_Regardless_Of_Their_Order()
    {
        RoutePlaceholderDateFormat.TryGetRange(new[] { ("MM", "12"), ("yyyy", "2025") }, out var start, out var endExclusive)
            .ShouldBeTrue();

        start.ShouldBe(new DateTime(2025, 12, 1));
        endExclusive.ShouldBe(new DateTime(2026, 1, 1));
    }

    [Fact]
    public void Should_Return_Whole_Year_For_A_Lone_Year_Part()
    {
        RoutePlaceholderDateFormat.TryGetRange(new[] { ("yyyy", "2025") }, out var start, out var endExclusive)
            .ShouldBeTrue();

        start.ShouldBe(new DateTime(2025, 1, 1));
        endExclusive.ShouldBe(new DateTime(2026, 1, 1));
    }

    /// <summary>
    /// A period has to be spelled out from the year down without a gap - otherwise the parser silently
    /// fills in the missing unit (the current year, January) and the filter means something the URL never
    /// said.
    /// </summary>
    [Theory]
    [InlineData("MM", "08")]
    [InlineData("dd", "15")]
    [InlineData("yyyy-dd", "2026-15")]
    [InlineData("MM-dd", "08-15")]
    public void Should_Fail_For_An_Incomplete_Date(string format, string capturedValue)
    {
        RoutePlaceholderDateFormat.TryGetRange(format, capturedValue, out _, out _).ShouldBeFalse();
        RoutePlaceholderDateFormat.TryGetRange(new[] { (format, capturedValue) }, out _, out _).ShouldBeFalse();
    }

    [Fact]
    public void Should_Fail_When_Parts_Do_Not_Form_A_Valid_Date()
    {
        RoutePlaceholderDateFormat.TryGetRange(new[] { ("yyyy", "2026"), ("MM", "13") }, out _, out _).ShouldBeFalse();

        // The same unit named twice, with two different values, is no one date either.
        RoutePlaceholderDateFormat.TryGetRange(new[] { ("yyyy", "2026"), ("yyyy-MM", "2025-08") }, out _, out _)
            .ShouldBeFalse();
    }

    [Theory]
    [InlineData("yyyy", true)]
    [InlineData("MM", true)]
    [InlineData("yyyy-MM-dd", true)]
    [InlineData("0000", false)]
    [InlineData("'MM'", false)]
    public void Should_Tell_A_Date_Format_From_Any_Other(string format, bool expected)
    {
        RoutePlaceholderDateFormat.IsDateFormat(format).ShouldBe(expected);
    }

    [Fact]
    public void Should_Fail_For_No_Parts_At_All()
    {
        RoutePlaceholderDateFormat.TryGetRange(Array.Empty<(string, string)>(), out _, out _).ShouldBeFalse();
    }
}
