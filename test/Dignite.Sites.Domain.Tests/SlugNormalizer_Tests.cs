using System.Text;
using Shouldly;
using Xunit;

namespace Dignite.Sites;

public class SlugNormalizer_Tests
{
    [Theory]
    [InlineData("Hello World", "hello-world")]
    [InlineData("  Hello   World  ", "hello-world")]
    [InlineData("A  --  B!!", "a-b")]
    [InlineData("Already-A-Slug", "already-a-slug")]
    public void Should_Normalize_Latin_Text(string input, string expected)
    {
        SlugNormalizer.Normalize(input).ShouldBe(expected);
    }

    /// <summary>
    /// Diacritics fold to their base letter, which is the long-standing convention for Latin scripts and
    /// keeps <c>café</c> and <c>cafe</c> from being two different URLs.
    /// </summary>
    [Fact]
    public void Should_Fold_Latin_Diacritics()
    {
        SlugNormalizer.Normalize("Café Déjà Vu").ShouldBe("cafe-deja-vu");
    }

    /// <summary>
    /// The regression this guards is the whole reason the normalizer is configured rather than taken as
    /// it comes: Slugify's default deny-list is ASCII-only, so every one of these produced an empty
    /// string or a bare "-". That is not a cosmetic failure - an empty slug is the slug of a page's
    /// single content, so a Chinese, Russian, Japanese, Greek or Arabic title would have silently
    /// produced a content claiming its page's own URL.
    /// </summary>
    [Theory]
    [InlineData("我的旅行", "我的旅行")]
    [InlineData("日本語のタイトル", "日本語のタイトル")]
    [InlineData("Ελληνικά", "ελληνικα")]
    [InlineData("한국어 제목", "한국어-제목")]
    public void Should_Keep_Non_Latin_Scripts(string input, string expected)
    {
        SlugNormalizer.Normalize(input).ShouldBe(expected);
    }

    [Fact]
    public void Should_Keep_Words_Of_Non_Latin_Scripts_Separated()
    {
        SlugNormalizer.Normalize("Мой отпуск").ShouldContain("-");
        SlugNormalizer.Normalize("مرحبا بالعالم").ShouldBe("مرحبا-بالعالم");
    }

    /// <summary>
    /// The output has to be NFC, the form a browser sends. Slugify decomposes to NFD to strip Latin
    /// diacritics, and for a script whose decomposition yields letters rather than combining marks -
    /// Hangul - nothing recomposes them, so the slug would render correctly and still never match the
    /// URL a visitor requests. The two forms are also distinct strings to the unique constraint, so the
    /// same visible slug could be stored twice.
    /// </summary>
    [Fact]
    public void Should_Return_Nfc_Normalized_Output()
    {
        var slug = SlugNormalizer.Normalize("한국어 제목");

        slug.IsNormalized(NormalizationForm.FormC).ShouldBeTrue();
        slug.ShouldBe("한국어-제목".Normalize(NormalizationForm.FormC));
    }

    [Fact]
    public void Should_Keep_Digits()
    {
        SlugNormalizer.Normalize("Top 10 Tips").ShouldBe("top-10-tips");
    }

    /// <summary>
    /// Empty is a legitimate slug - the single content of a home or "about" page - so Normalize returns
    /// it rather than throwing. TryNormalize is what a caller deriving a slug from a title uses, so that
    /// "nothing survived" is distinguishable from "the caller meant empty".
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("?!?")]
    public void Should_Report_When_Nothing_Usable_Survives(string? input)
    {
        SlugNormalizer.Normalize(input).ShouldBe("");
        SlugNormalizer.TryNormalize(input, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryNormalize_Should_Report_Success_With_Value()
    {
        SlugNormalizer.TryNormalize("My Trip", out var slug).ShouldBeTrue();
        slug.ShouldBe("my-trip");
    }
}
