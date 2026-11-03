using System.Linq;
using Shouldly;
using Xunit;

namespace Dignite.Site.Themes;

public class SiteThemePaths_Tests
{
    [Theory]
    [InlineData(" /layouts/default ", "layouts/default")]
    [InlineData("Events", "Events")]
    [InlineData("/assets/site.css", "assets/site.css")]
    public void Should_Normalize(string path, string expected)
    {
        SiteThemePaths.Normalize(path).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Events")]
    [InlineData("blog/post")]
    [InlineData("_card")]
    [InlineData("blog/_sidebar")]
    [InlineData("layouts/default")]
    [InlineData("assets/site.css")]
    [InlineData("js/menu.min.js")]
    [InlineData("a-b/c_d/e-f.json")]
    public void Should_Accept_A_Valid_Path(string path)
    {
        SiteThemePaths.IsValid(path).ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("a//b")]
    [InlineData("a/")]
    [InlineData("../x")]
    [InlineData("a/../b")]
    [InlineData(".hidden")]
    [InlineData("a/.hidden")]
    [InlineData("dir.v2/file")]
    [InlineData("-dash")]
    [InlineData("a b")]
    [InlineData("site.")]
    [InlineData("a\\b")]
    public void Should_Refuse_An_Invalid_Path(string path)
    {
        SiteThemePaths.IsValid(path).ShouldBeFalse();
    }

    [Fact]
    public void Should_Refuse_A_Path_Longer_Than_The_Limit()
    {
        SiteThemePaths.IsValid(new string('a', SiteThemeConsts.MaxPathLength + 1)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("Events", false, null)]
    [InlineData("_card", false, null)]
    [InlineData("layouts/default", false, null)]
    [InlineData("assets/site.css", true, "css")]
    [InlineData("js/menu.min.js", true, "js")]
    [InlineData("Events.ja", false, "ja")]
    [InlineData("blog/post.zh-Hant", false, "zh-Hant")]
    public void Should_Tell_A_Template_From_A_Static_File_By_Its_Extension(string path, bool isAsset, string? extension)
    {
        SiteThemePaths.IsAsset(path).ShouldBe(isAsset);
        SiteThemePaths.IsTemplate(path).ShouldBe(!isAsset);
        SiteThemePaths.GetExtension(path).ShouldBe(extension);
    }

    [Theory]
    [InlineData("Events", true)]
    [InlineData("assets/site.css", true)]
    [InlineData("assets/site.CSS", true)]
    [InlineData("data/feed.xml", true)]
    [InlineData("post.html", false)]
    [InlineData("post.sbn", false)]
    [InlineData("image.png", false)]
    [InlineData("a//b", false)]
    [InlineData("Events.ja", true)]
    [InlineData("blog/post.zh-Hans", true)]
    [InlineData("post.tpl", false)]
    public void Should_Allow_Templates_And_Known_Static_Files_Only(string path, bool expected)
    {
        SiteThemePaths.IsAllowed(path).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Events.ja", "Events", "ja")]
    [InlineData("blog/post.zh-Hant", "blog/post", "zh-Hant")]
    [InlineData("layouts/default.en-GB", "layouts/default", "en-GB")]
    public void Should_Read_The_Version_Of_A_Template_For_One_Language(string path, string templatePath, string cultureName)
    {
        SiteThemePaths.TryGetLanguageVersion(path, out var template, out var culture).ShouldBeTrue();
        template.ShouldBe(templatePath);
        culture.ShouldBe(cultureName);
        SiteThemePaths.GetLanguageVersionPath(templatePath, cultureName).ShouldBe(path);
        SiteThemePaths.IsLanguageVersion(path).ShouldBeTrue();
    }

    /// <summary>
    /// A static file's extension, a second dot, a language the runtime does not know and one spelled another way
    /// are none of them a language version: they stay what they were - a static file, or refused.
    /// </summary>
    [Theory]
    [InlineData("Events")]
    [InlineData("assets/site.css")]
    [InlineData("js/menu.min.js")]
    [InlineData("Events.min.ja")]
    [InlineData("post.html")]
    [InlineData("image.png")]
    [InlineData("post.tpl")]
    [InlineData("post.sbn")]
    [InlineData("Events.zh-hans")]
    [InlineData("Events.JA")]
    [InlineData("Events.")]
    [InlineData(".ja")]
    public void Should_Not_Take_Anything_Else_For_A_Language_Version(string path)
    {
        SiteThemePaths.IsLanguageVersion(path).ShouldBeFalse();
    }

    [Fact]
    public void Should_Hash_The_Utf8_Bytes_As_Lower_Case_Hex()
    {
        var hash = SiteThemePaths.ComputeContentHash("body");

        hash.Length.ShouldBe(SiteThemeConsts.ContentHashLength);
        hash.ShouldBe(hash.ToLowerInvariant());
        hash.ShouldBe(System.Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("body"))).ToLowerInvariant());
        SiteThemePaths.ComputeContentHash("bodY").ShouldNotBe(hash);
    }

    [Fact]
    public void Should_Keep_Every_Template_Path_A_Valid_Page_Template()
    {
        // A template path is what a page's Template holds, so it must pass that rule too.
        foreach (var path in new[] { "Events", "blog/post", "_card", "layouts/default" })
        {
            System.Text.RegularExpressions.Regex.IsMatch(path, Pages.PageConsts.TemplatePattern).ShouldBeTrue(path);
        }

        new[] { "Events", "blog/post" }.All(SiteThemePaths.IsTemplate).ShouldBeTrue();
    }
}
