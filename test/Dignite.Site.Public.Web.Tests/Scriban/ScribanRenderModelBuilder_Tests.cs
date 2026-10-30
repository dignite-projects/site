using System;
using System.Threading.Tasks;
using global::Scriban.Runtime;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Scriban;

/// <summary>The template vocabulary: what a Scriban template sees of a page, and in which shape.</summary>
public class ScribanRenderModelBuilder_Tests
{
    [Fact]
    public async Task Dto_Members_Should_Be_Snake_Case()
    {
        var globals = await BuildAsync(ScribanTestRenderer.ContentViewModel());

        var page = Get<ScriptObject>(globals, "page");
        page["display_name"].ShouldBe("About us");
        page["is_home_page"].ShouldBe(false);
        page.ContainsKey("content_types").ShouldBeFalse();

        var content = Get<ScriptObject>(globals, "content");
        content["slug"].ShouldBe("my-trip");
        content["status"].ShouldBe("published");
        content.ContainsKey("field_values").ShouldBeFalse();

        Get<ScriptObject>(globals, "content_type")["name"].ShouldBe("post");

        var head = Get<ScriptObject>(globals, "head");
        head["meta_title"].ShouldBe("About");
        head["canonical_url"].ShouldBe("https://acme.example/ja/about");
        var alternate = (ScriptObject)((ScriptArray)head["hreflang_alternates"]!)[1]!;
        alternate["culture_name"].ShouldBe("ja");
    }

    [Fact]
    public async Task Fields_Should_Keep_Their_Names_And_Nested_Values()
    {
        var content = Get<ScriptObject>(await BuildAsync(ScribanTestRenderer.ContentViewModel()), "content");
        var fields = (ScriptObject)content["fields"]!;

        fields["Title"].ShouldBe("My trip");
        fields["view_count"].ShouldBe(42);

        var gallery = (ScriptArray)fields["Gallery"]!;
        ((ScriptObject)gallery[0]!)["url"].ShouldBe("/a.png");

        // A CLR value is serialized first, so it has the shape it would have over HTTP.
        var tags = (ScriptArray)fields["Tags"]!;
        tags.Count.ShouldBe(2);
        tags[1].ShouldBe("japan");
    }

    [Fact]
    public async Task Times_Should_Be_Dates()
    {
        var globals = await BuildAsync(ScribanTestRenderer.PageViewModel());

        Get<ScriptObject>(globals, "page")["creation_time"].ShouldBe(new DateTime(2026, 1, 2, 3, 4, 5));
        globals["published_after"].ShouldBe(new DateTime(2026, 1, 1));

        var content = Get<ScriptObject>(await BuildAsync(ScribanTestRenderer.ContentViewModel()), "content");
        content["publish_time"].ShouldBe(new DateTime(2026, 8, 15, 9, 30, 0));
        content["last_modification_time"].ShouldBeNull();
    }

    [Fact]
    public async Task Field_List_Should_Follow_The_Content_Type_Order()
    {
        var globals = await BuildAsync(ScribanTestRenderer.ContentViewModel());

        var fieldList = Get<ScriptArray>(globals, "field_list");
        fieldList.Count.ShouldBe(3);

        var first = (ScriptObject)fieldList[0]!;
        first["name"].ShouldBe("Title");
        first["display_name"].ShouldBe("Title");
        first["field_type_name"].ShouldBe("Text");
        first["required"].ShouldBe(true);
        first["value"].ShouldBe("My trip");

        ((ScriptObject)fieldList[1]!)["name"].ShouldBe("Tags");
        ((ScriptObject)fieldList[2]!)["name"].ShouldBe("Gallery");

        Get<ScriptObject>(globals, "content")["field_list"].ShouldBeSameAs(fieldList);
    }

    [Fact]
    public async Task Languages_Should_Offer_The_Pages_Translations()
    {
        var globals = await BuildAsync(ScribanTestRenderer.PageViewModel());

        globals["culture"].ShouldBe("ja");
        globals["default_culture"].ShouldBe("en");

        var languages = Get<ScriptArray>(globals, "languages");
        languages.Count.ShouldBe(2);

        var english = (ScriptObject)languages[0]!;
        english["culture_name"].ShouldBe("en");
        english["url"].ShouldBe("/about");
        english["is_current"].ShouldBe(false);
        english["is_translation"].ShouldBe(true);

        ((ScriptObject)languages[1]!)["is_current"].ShouldBe(true);
    }

    [Fact]
    public async Task A_Page_Match_Should_Have_No_Content()
    {
        var globals = await BuildAsync(ScribanTestRenderer.PageViewModel());

        globals.ContainsKey("content").ShouldBeTrue();
        globals["content"].ShouldBeNull();
        globals["content_type"].ShouldBeNull();
        Get<ScriptArray>(globals, "field_list").Count.ShouldBe(0);

        Get<ScriptObject>(globals, "filter_values")["publishTime:yyyy"].ShouldBe("2026");
        Get<ScriptObject>(globals, "field_filters")["category"].ShouldBe("engineering");
    }

    private static Task<ScriptObject> BuildAsync(Routing.SiteRenderViewModel viewModel)
    {
        return new ScribanTestRenderer().ModelBuilder.BuildGlobalsAsync(viewModel);
    }

    private static T Get<T>(ScriptObject globals, string name)
    {
        return globals[name].ShouldBeOfType<T>();
    }
}
