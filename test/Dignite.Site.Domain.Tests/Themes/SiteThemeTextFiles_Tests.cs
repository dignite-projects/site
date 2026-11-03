using System;
using Shouldly;
using Xunit;

namespace Dignite.Site.Themes;

public class SiteThemeTextFiles_Tests
{
    [Theory]
    [InlineData("locales/ja.json", true)]
    [InlineData("locales/zh-Hans.json", true)]
    [InlineData("locales/JA.JSON", true)]
    [InlineData("locales/ja.css", false)]
    [InlineData("locales/.json", false)]
    [InlineData("locales/sub/ja.json", false)]
    [InlineData("Locales/ja.json", false)]
    [InlineData("assets/locales/ja.json", false)]
    [InlineData("ja.json", false)]
    [InlineData("locales/ja", false)]
    public void Should_Tell_A_Text_File_By_Its_Path(string path, bool isTextFile)
    {
        SiteThemeTextFiles.IsTextFile(path).ShouldBe(isTextFile);
    }

    [Fact]
    public void Should_Read_The_Texts_Of_A_Language()
    {
        var result = SiteThemeTextFiles.Read("locales/ja.json",
            "{ \"culture\": \"ja\", \"texts\": { \"Blog:Name\": \"ブログ\", \"Archive:Month\": \"{0:yyyy年M月}\" } }");

        result.IsValid.ShouldBeTrue();
        result.CultureName.ShouldBe("ja");
        result.Texts!["Blog:Name"].ShouldBe("ブログ");
        result.Texts["Archive:Month"].ShouldBe("{0:yyyy年M月}");
    }

    [Fact]
    public void Should_Allow_Comments_And_Trailing_Commas_As_ABPs_Own_Files_Do()
    {
        var result = SiteThemeTextFiles.Read("locales/en.json",
            "// the English texts\n{ \"culture\": \"en\", \"texts\": { \"A\": \"a\", /* b */ \"B\": \"b\", }, }");

        result.IsValid.ShouldBeTrue();
        result.Texts!.Count.ShouldBe(2);
    }

    [Fact]
    public void Should_Name_The_Language_In_Its_Canonical_Spelling()
    {
        var result = SiteThemeTextFiles.Read("locales/zh-hans.json", "{ \"culture\": \"zh-hans\", \"texts\": {} }");

        result.IsValid.ShouldBeTrue();
        result.CultureName.ShouldBe("zh-Hans");
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("[]")]
    [InlineData("{ \"texts\": {} }")]
    [InlineData("{ \"culture\": \"ja\" }")]
    [InlineData("{ \"culture\": \"ja\", \"texts\": [] }")]
    [InlineData("{ \"culture\": \"ja\", \"texts\": { \"A\": 1 } }")]
    [InlineData("{ \"culture\": \"\", \"texts\": {} }")]
    [InlineData("")]
    public void Should_Report_A_File_That_Is_Not_Valid(string body)
    {
        var result = SiteThemeTextFiles.Read("locales/ja.json", body);

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldNotBeNullOrWhiteSpace();
        result.CultureName.ShouldBeNull();
        result.Texts.ShouldBeNull();
    }

    /// <summary>A copied en.json edited into Japanese but not renamed must not stand in for the English texts.</summary>
    [Fact]
    public void Should_Report_A_File_Whose_Culture_Is_Not_The_One_Its_Name_Says()
    {
        var result = SiteThemeTextFiles.Read("locales/en.json", "{ \"culture\": \"ja\", \"texts\": {} }");

        result.IsValid.ShouldBeFalse();
        result.Error.ShouldContain("\"ja\"");
        result.Error.ShouldContain("\"en\"");
    }

    [Fact]
    public void Should_Refuse_To_Read_A_File_That_Is_Not_A_Text_File()
    {
        Should.Throw<ArgumentException>(() => SiteThemeTextFiles.Read("assets/site.css", "{}"));
    }
}
