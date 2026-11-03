using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Site.Public.Themes;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// The version of a template for one language (<c>Default.ja</c> for <c>Default</c>): a page, its layout and what it
/// includes are each rendered in the version for the page's language when the theme has one, else as they are.
/// </summary>
public class ScribanLanguageVersions_Tests
{
    private const string Layout = "{{ site_head }}{{ body }}";

    private static SiteThemeRenderSet Theme(Dictionary<string, string> files)
    {
        if (!files.ContainsKey(ScribanTestRenderer.LayoutPath))
        {
            files[ScribanTestRenderer.LayoutPath] = Layout;
        }

        return ScribanTestRenderer.RenderSet(files, ScribanTestRenderer.LayoutPath);
    }

    private static Task<ScribanPageRender> RenderAsync(Dictionary<string, string> files, string cultureName)
    {
        return new ScribanTestRenderer("en,ja,zh-Hant,zh-Hans").RenderPageAsync(
            Theme(files),
            ScribanTestRenderer.PageViewModel(cultureName: cultureName));
    }

    [Fact]
    public async Task A_Page_Should_Render_The_Version_Of_Its_Template_For_Its_Language()
    {
        var files = new Dictionary<string, string> { ["Default"] = "[base]", ["Default.ja"] = "[ja]" };

        var japanese = await RenderAsync(files, "ja");
        var english = await RenderAsync(files, "en");

        japanese.Html.ShouldEndWith("[ja]");
        japanese.TemplatePath.ShouldBe("Default.ja");
        english.Html.ShouldEndWith("[base]");
        english.TemplatePath.ShouldBe("Default");
    }

    [Fact]
    public async Task A_Language_Without_A_Version_Should_Render_The_Template_Itself()
    {
        var render = await RenderAsync(new() { ["Default"] = "[base]", ["Default.ja"] = "[ja]" }, "zh-Hans");

        render.Html.ShouldEndWith("[base]");
        render.TemplatePath.ShouldBe("Default");
    }

    /// <summary>zh-Hant-TW looks for Default.zh-Hant-TW, Default.zh-Hant, Default.zh: the most specific version wins.</summary>
    [Fact]
    public async Task A_Version_For_A_Parent_Language_Should_Do_And_The_Most_Specific_One_Should_Win()
    {
        var onlyChinese = new Dictionary<string, string> { ["Default"] = "[base]", ["Default.zh"] = "[zh]" };
        var withTraditional = new Dictionary<string, string>
        {
            ["Default"] = "[base]",
            ["Default.zh"] = "[zh]",
            ["Default.zh-Hant"] = "[zh-Hant]"
        };

        (await RenderAsync(onlyChinese, "zh-Hant-TW")).Html.ShouldEndWith("[zh]");
        (await RenderAsync(withTraditional, "zh-Hant-TW")).Html.ShouldEndWith("[zh-Hant]");
        (await RenderAsync(withTraditional, "zh-Hans-CN")).Html.ShouldEndWith("[zh]");
    }

    /// <summary>The site's default language is not looked for: its version is the template itself.</summary>
    [Fact]
    public async Task The_Sites_Default_Language_Should_Not_Be_Tried_For_Another_Language()
    {
        // The site is "en,ja": English is the default. A page in French does not borrow Default.en.
        var render = await RenderAsync(new() { ["Default"] = "[base]", ["Default.en"] = "[en]" }, "fr");

        render.Html.ShouldEndWith("[base]");
    }

    [Fact]
    public async Task A_Layout_Should_Render_In_The_Version_For_The_Pages_Language()
    {
        var files = new Dictionary<string, string>
        {
            ["Default"] = "[page]",
            [ScribanTestRenderer.LayoutPath] = "{{ site_head }}<base>{{ body }}</base>",
            [ScribanTestRenderer.LayoutPath + ".ja"] = "{{ site_head }}<ja>{{ body }}</ja>"
        };

        var japanese = await RenderAsync(files, "ja");
        var english = await RenderAsync(files, "en");

        japanese.Html.ShouldEndWith("<ja>[page]</ja>");
        japanese.LayoutPath.ShouldBe("layouts/default.ja");
        english.Html.ShouldEndWith("<base>[page]</base>");
        english.LayoutPath.ShouldBe("layouts/default");
    }

    /// <summary>A version is a template of its own: it may choose another layout, or none, by its own front matter.</summary>
    [Fact]
    public async Task A_Version_Should_Choose_Its_Layout_By_Its_Own_Front_Matter()
    {
        var files = new Dictionary<string, string>
        {
            ["Default"] = "[page]",
            ["Default.ja"] = "+++\nlayout = \"none\"\n+++\n{{ site_head }}[ja only]"
        };

        var japanese = await RenderAsync(files, "ja");
        var english = await RenderAsync(files, "en");

        japanese.Html.ShouldEndWith("[ja only]");
        japanese.LayoutPath.ShouldBeNull();
        english.LayoutPath.ShouldBe("layouts/default");
    }

    [Fact]
    public async Task An_Include_Should_Render_The_Version_For_The_Pages_Language()
    {
        var files = new Dictionary<string, string>
        {
            ["Default"] = "[{{ include \"blog/_card\" }}]",
            ["blog/_card"] = "base card, {{ include \"_tag\" }}",
            ["blog/_card.ja"] = "ja card, {{ include \"_tag\" }}",
            ["_tag"] = "base tag",
            ["_tag.ja"] = "ja tag"
        };

        (await RenderAsync(files, "ja")).Html.ShouldEndWith("[ja card, ja tag]");
        (await RenderAsync(files, "en")).Html.ShouldEndWith("[base card, base tag]");
    }

    /// <summary>Naming a version includes exactly that file, whatever language the page is in.</summary>
    [Fact]
    public async Task An_Include_Of_A_Version_By_Name_Should_Render_That_Version()
    {
        var files = new Dictionary<string, string>
        {
            ["Default"] = "[{{ include \"_card.ja\" }}]",
            ["_card"] = "base",
            ["_card.ja"] = "ja"
        };

        (await RenderAsync(files, "en")).Html.ShouldEndWith("[ja]");
    }

    [Fact]
    public async Task A_Theme_With_No_Versions_Should_Render_As_It_Always_Did()
    {
        var render = await RenderAsync(new() { ["Default"] = "[base]" }, "ja");

        render.Html.ShouldEndWith("[base]");
        render.TemplatePath.ShouldBe("Default");
    }

    [Fact]
    public void The_Render_Set_Should_Not_Take_A_Version_For_A_Template()
    {
        var renderSet = ScribanTestRenderer.RenderSet(new Dictionary<string, string> { ["Default.ja"] = "[ja]" });

        renderSet.FindTemplate("Default", new[] { "ja" }).ShouldBeNull();
        renderSet.FindTemplate("Default.ja")!.Body.ShouldBe("[ja]");
    }

    [Fact]
    public void The_Render_Set_Should_Look_Up_A_Template_Once_When_The_Theme_Has_No_Versions()
    {
        var renderSet = ScribanTestRenderer.RenderSet(new Dictionary<string, string> { ["Default"] = "[base]" });

        renderSet.FindTemplate("Default", new[] { "ja", "en" }).ShouldBeSameAs(renderSet.FindTemplate("Default"));
        renderSet.FindTemplate("Default", null).ShouldBeSameAs(renderSet.FindTemplate("Default"));
    }
}
