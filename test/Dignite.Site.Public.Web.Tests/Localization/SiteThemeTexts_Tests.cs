using System.Collections.Generic;
using Dignite.Site.Public.Scriban;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Localization;

/// <summary>
/// A Scriban theme's own texts: the catalog parsed from its <c>locales/*.json</c> files, and the language chain
/// <c>t</c> looks them up in. How <c>t</c> renders them is in <c>ScribanPageRenderer_Tests</c>.
/// </summary>
public class SiteThemeTexts_Tests
{
    private static SiteThemeTextCatalog Theme(params (string Culture, string Texts)[] files)
    {
        var snapshot = new List<Dignite.Site.Themes.SiteThemeSnapshotFileDto>();
        foreach (var (culture, texts) in files)
        {
            snapshot.Add(ScribanTestRenderer.File(
                $"locales/{culture}.json",
                "{\"culture\": \"" + culture + "\", \"texts\": {" + texts + "}}"));
        }

        return SiteThemeTextCatalog.Create(snapshot);
    }

    [Theory]
    [InlineData("zh-Hant-TW", "en-GB", new[] { "zh-Hant-TW", "zh-Hant", "zh", "en-GB", "en" })]
    [InlineData("ja", "en", new[] { "ja", "en" })]
    [InlineData("en-GB", "en", new[] { "en-GB", "en" })]
    [InlineData("ja", "ja", new[] { "ja" })]
    [InlineData("ja", null, new[] { "ja" })]
    [InlineData("ja", " ", new[] { "ja" })]
    public void The_Chain_Should_Go_Through_Parents_To_The_Default_Language_Without_Repeats(
        string culture, string? defaultCulture, string[] expected)
    {
        SiteTemplateCultureChain.Create(culture, defaultCulture).ShouldBe(expected);
    }

    [Fact]
    public void Find_Should_Take_The_First_Language_Of_The_Chain_That_Has_The_Key()
    {
        var theme = Theme(
            ("zh", "\"OnlyInZh\": \"中文\", \"Both\": \"zh\""),
            ("zh-Hant", "\"Both\": \"hant\""),
            ("en", "\"OnlyInEn\": \"English\""));

        var chain = SiteTemplateCultureChain.Create("zh-Hant-TW", "en");

        theme.Find(chain, "Both").ShouldBe("hant");
        theme.Find(chain, "OnlyInZh").ShouldBe("中文");
        theme.Find(chain, "OnlyInEn").ShouldBe("English");
        theme.Find(chain, "Nowhere").ShouldBeNull();
    }

    [Fact]
    public void A_Catalog_Should_Take_Only_The_Valid_Files_In_The_Locales_Folder()
    {
        var catalog = SiteThemeTextCatalog.Create(new[]
        {
            ScribanTestRenderer.File("locales/en.json", "{\"culture\": \"en\", \"texts\": {\"A\": \"a\"}}"),
            ScribanTestRenderer.File("locales/ja.json", "{ not json"),
            ScribanTestRenderer.File("locales/fr.json", "{\"culture\": \"en\", \"texts\": {\"A\": \"wrong\"}}"),
            ScribanTestRenderer.File("assets/de.json", "{\"culture\": \"de\", \"texts\": {\"A\": \"not a text file\"}}"),
            ScribanTestRenderer.File("Default", "{{ t \"A\" }}")
        });

        catalog.GetOrNull("en", "A").ShouldBe("a");
        catalog.GetOrNull("ja", "A").ShouldBeNull();
        catalog.GetOrNull("fr", "A").ShouldBeNull();
        catalog.GetOrNull("de", "A").ShouldBeNull();
    }

    [Fact]
    public void A_Catalog_Should_Keep_The_First_Path_Of_A_Language()
    {
        // The check refuses a theme with both; a catalog still has to pick one, and picks the same every time.
        // Ordinally "locales/JA.json" comes before "locales/ja.json".
        var catalog = SiteThemeTextCatalog.Create(new[]
        {
            ScribanTestRenderer.File("locales/ja.json", "{\"culture\": \"ja\", \"texts\": {\"A\": \"loses\"}}"),
            ScribanTestRenderer.File("locales/JA.json", "{\"culture\": \"JA\", \"texts\": {\"A\": \"wins\"}}")
        });

        catalog.GetOrNull("ja", "A").ShouldBe("wins");
    }

    [Fact]
    public void A_Theme_Without_Texts_Should_Have_The_Empty_Catalog()
    {
        ScribanTestRenderer.RenderSet("<p>x</p>").Texts.ShouldBeSameAs(SiteThemeTextCatalog.Empty);
    }

    [Fact]
    public void A_Render_Set_Should_Parse_Its_Texts_Once()
    {
        var renderSet = ScribanTestRenderer.RenderSet(
            new Dictionary<string, string>
            {
                ["Default"] = "<p>x</p>",
                ["locales/ja.json"] = "{\"culture\": \"ja\", \"texts\": {\"A\": \"a\"}}"
            });

        renderSet.Texts.ShouldBeSameAs(renderSet.Texts);
        renderSet.Texts.GetOrNull("ja", "A").ShouldBe("a");
    }
}
