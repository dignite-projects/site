using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Templating;

public class LocalizerTemplateExtensions_Tests
{
    private static readonly Dictionary<string, string> Texts = new()
    {
        ["Field:blog_category:engineering"] = "Engineering",
        ["Emphasis"] = "<em>Read</em>"
    };

    [Fact]
    public void Should_Return_The_Text_When_There_Is_One()
    {
        new DictionaryLocalizer().GetOrDefault("Field:blog_category:engineering", "研发").ShouldBe("Engineering");
    }

    [Fact]
    public void Should_Return_The_Fallback_When_The_Text_Is_Missing()
    {
        new DictionaryLocalizer().GetOrDefault("Field:blog_category:essays", "随笔").ShouldBe("随笔");
    }

    [Fact]
    public void Should_Return_The_Key_When_There_Is_No_Fallback_Either()
    {
        new DictionaryLocalizer().GetOrDefault("Field:blog_category:essays", null).ShouldBe("Field:blog_category:essays");
    }

    [Fact]
    public void Html_Form_Should_Write_A_Found_Text_As_The_Localizer_Does()
    {
        Render(new HtmlLocalizer(new DictionaryLocalizer()).GetOrDefault("Emphasis", "fallback")).ShouldBe("<em>Read</em>");
    }

    [Fact]
    public void Html_Form_Should_Encode_The_Fallback()
    {
        Render(new HtmlLocalizer(new DictionaryLocalizer()).GetOrDefault("Missing", "R&D <team>"))
            .ShouldBe("R&amp;D &lt;team&gt;");
    }

    private static string Render(IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    private class DictionaryLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] =>
            Texts.TryGetValue(name, out var value)
                ? new LocalizedString(name, value)
                : new LocalizedString(name, name, resourceNotFound: true);

        public LocalizedString this[string name, params object[] arguments] => this[name];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
    }
}
