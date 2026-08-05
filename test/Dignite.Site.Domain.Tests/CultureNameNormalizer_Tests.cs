using System;
using Shouldly;
using Xunit;

namespace Dignite.Site;

/// <summary>
/// The culture name is simultaneously a URL prefix, an hreflang value, part of a unique constraint and
/// part of the translation-group key. These tests pin the normalization that keeps those four from
/// disagreeing (总体设计 §2.4).
/// </summary>
public class CultureNameNormalizer_Tests
{
    [Theory]
    [InlineData("en", "en")]
    [InlineData("EN", "en")]
    [InlineData("  en  ", "en")]
    [InlineData("en-gb", "en-GB")]
    [InlineData("EN-GB", "en-GB")]
    [InlineData("zh-hans", "zh-Hans")]
    [InlineData("ZH-HANS", "zh-Hans")]
    public void Should_Normalize_To_Canonical_Form(string input, string expected)
    {
        CultureNameNormalizer.Normalize(input).ShouldBe(expected);
    }

    /// <summary>
    /// The failure this exists to prevent: two spellings of one language reaching the table as two rows,
    /// splitting a translation group in half and emitting two unrelated sets of hreflang links.
    /// </summary>
    [Fact]
    public void Should_Collapse_Different_Spellings_Of_One_Culture()
    {
        CultureNameNormalizer.Normalize("zh-CN")
            .ShouldBe(CultureNameNormalizer.Normalize("zh-cn"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public void Should_Reject_Unusable_Input(string? input)
    {
        Should.Throw<ArgumentException>(() => CultureNameNormalizer.Normalize(input!));
        CultureNameNormalizer.TryNormalize(input, out _).ShouldBeFalse();
    }

    /// <summary>
    /// Regression guard for the <c>predefinedOnly</c> flag. Without it .NET does not reject an
    /// unrecognized tag - it invents a custom culture out of whatever prefix parses, turning
    /// "not-a-culture-at-all" into the entirely plausible "not" and accepting "xx-YY" verbatim. Both
    /// would then be stored and served as real languages, so a single typo in an MCP call would create a
    /// phantom translation that splits the content's language group.
    /// </summary>
    [Theory]
    [InlineData("not-a-culture-at-all")]
    [InlineData("xx")]
    [InlineData("xx-YY")]
    public void Should_Reject_Culture_Dotnet_Would_Otherwise_Invent(string input)
    {
        Should.Throw<ArgumentException>(() => CultureNameNormalizer.Normalize(input));
        CultureNameNormalizer.TryNormalize(input, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryNormalize_Should_Report_Success_With_Value()
    {
        CultureNameNormalizer.TryNormalize("zh-hant", out var normalized).ShouldBeTrue();
        normalized.ShouldBe("zh-Hant");
    }
}
