using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Site.Public.Localization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.TagHelpers;

public class SiteLinkTagHelper_Tests
{
    private static async Task<object?> ProcessAsync(string path, object href, string tagName = "a", string attributeName = "href")
    {
        var host = new SiteLanguageTestHost("en,ja", path);
        var tagHelper = new SiteLinkTagHelper(host.LanguageContext, host.Options);

        var attributes = new TagHelperAttributeList { { attributeName, href } };
        var context = new TagHelperContext(tagName, attributes, new Dictionary<object, object>(), "unique");
        var output = new TagHelperOutput(tagName, attributes, (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        await tagHelper.ProcessAsync(context, output);

        return output.Attributes[attributeName].Value;
    }

    /// <summary>Every attribute that navigates, not only a link's - a search form on /ja searches in Japanese.</summary>
    [Theory]
    [InlineData("area", "href")]
    [InlineData("form", "action")]
    [InlineData("button", "formaction")]
    [InlineData("input", "formaction")]
    public async Task Should_Localize_Every_Navigating_Attribute(string tagName, string attributeName)
    {
        (await ProcessAsync("/ja", new HtmlString("~/search"), tagName, attributeName)).ShouldBe("~/ja/search");
    }

    [Fact]
    public async Task Should_Localize_A_Literal_Link_On_A_Prefixed_Page()
    {
        (await ProcessAsync("/ja/about", new HtmlString("~/services"))).ShouldBe("~/ja/services");
    }

    [Fact]
    public async Task Should_Leave_A_Link_On_A_Default_Language_Page_As_It_Was()
    {
        var href = new HtmlString("~/services");

        (await ProcessAsync("/about", href)).ShouldBeSameAs(href);
    }

    [Fact]
    public async Task Should_Localize_A_String_Value()
    {
        (await ProcessAsync("/ja", "~/blog?tag=news")).ShouldBe("~/ja/blog?tag=news");
    }

    /// <summary>
    /// Razor hands "~/files/@name" over HTML-encoded; an encoded non-ASCII character ("&amp;#x62A5;") must not
    /// be read as a "#fragment" that hides the file extension.
    /// </summary>
    [Fact]
    public async Task Should_Read_An_Encoded_Value_Decoded()
    {
        (await ProcessAsync("/ja", new HtmlString("~/files/&#x62A5;&#x544A;.pdf"))).ShouldBeOfType<HtmlString>();
        (await ProcessAsync("/ja", new HtmlString("~/blog/&#x6211;&#x7684;?a=1&amp;b=2")))
            .ShouldBe("~/ja/blog/我的?a=1&b=2");
    }

    [Theory]
    [InlineData("/about")]
    [InlineData("https://example.com/")]
    public async Task Should_Leave_A_Link_That_Is_Not_App_Relative_Alone(string href)
    {
        var value = new HtmlString(href);

        (await ProcessAsync("/ja", value)).ShouldBeSameAs(value);
    }
}
