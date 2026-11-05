using Shouldly;
using Xunit;

namespace Dignite.Site.Seo;

public class OpenGraphLocale_Tests
{
    [Theory]
    [InlineData("en", "en_US")]
    [InlineData("ja", "ja_JP")]
    [InlineData("ko", "ko_KR")]
    [InlineData("en-GB", "en_GB")]
    [InlineData("pt-BR", "pt_BR")]
    [InlineData("es-419", "es_419")]
    [InlineData("zh", "zh_CN")]
    [InlineData("zh-Hans", "zh_CN")]
    [InlineData("zh-Hant", "zh_TW")]
    [InlineData("zh-Hant-HK", "zh_HK")]
    [InlineData("zh-Hans-SG", "zh_SG")]
    public void A_Language_Should_Be_Written_With_Its_Territory(string cultureName, string locale)
    {
        OpenGraphLocale.From(cultureName).ShouldBe(locale);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Nothing_Should_Be_No_Locale(string? cultureName)
    {
        OpenGraphLocale.From(cultureName).ShouldBeNull();
    }
}
