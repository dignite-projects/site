using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Select;
using Dignite.Site.ContentTypes;
using Dignite.Site.Fields;
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
        english["native_name"].ShouldBe("English");
        ((ScriptObject)languages[1]!)["native_name"].ShouldBe("日本語");

        ((ScriptObject)languages[1]!)["is_current"].ShouldBe(true);
    }

    /// <summary>The request's query parameters, each by its name in lower case; none on a request without a query string.</summary>
    [Fact]
    public async Task Query_Should_Hold_The_Requests_Parameters_By_Lower_Case_Name()
    {
        var viewModel = ScribanTestRenderer.PageViewModel();
        viewModel.Query = Routing.SiteRenderQuery.Parse("?Service=design&tab=");

        var query = Get<ScriptObject>(await BuildAsync(viewModel), "query");

        query["service"].ShouldBe("design");
        query["tab"].ShouldBe("");
        query.ContainsKey("Service").ShouldBeFalse();
        Get<ScriptObject>(await BuildAsync(ScribanTestRenderer.PageViewModel()), "query").Count.ShouldBe(0);
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

    [Fact]
    public void A_Field_Definition_Should_Be_Keyed_By_Name_With_Snake_Case_Members()
    {
        var field = new FieldDto
        {
            Id = Guid.NewGuid(),
            Name = "category",
            DisplayName = "Category",
            Description = "What the post is about",
            FieldTypeName = "Select"
        };

        var definitions = new ScribanTestRenderer().ModelBuilder.BuildFieldDefinitions(new[] { field });

        definitions.Count.ShouldBe(1);
        var category = definitions["category"].ShouldBeOfType<ScriptObject>();
        category["id"].ShouldBe(field.Id.ToString());
        category["name"].ShouldBe("category");
        category["display_name"].ShouldBe("Category");
        category["description"].ShouldBe("What the post is about");
        category["field_type_name"].ShouldBe("Select");
        category["configuration"].ShouldBeOfType<ScriptObject>().Count.ShouldBe(0);
    }

    [Fact]
    public void A_Configuration_Should_Keep_The_Names_It_Was_Stored_With()
    {
        // As storage hands it over: JSON, the keys carrying the field type's name, the members as stored.
        var stored = new FieldDto
        {
            Id = Guid.NewGuid(),
            Name = "category",
            DisplayName = "Category",
            FieldTypeName = "Select",
            Configuration = new Dictionary<string, object?>
            {
                [SelectConfigurationNames.Multiple] = true,
                [SelectConfigurationNames.Options] = JsonSerializer.SerializeToElement(
                    new[] { new { Text = "Engineering", Value = "engineering", Selected = false } })
            }
        };

        var configuration = Configuration(stored);

        configuration["Select.Multiple"].ShouldBe(true);
        var option = ((ScriptArray)configuration["Select.Options"]!)[0].ShouldBeOfType<ScriptObject>();
        option["Text"].ShouldBe("Engineering");
        option["Value"].ShouldBe("engineering");
        option["Selected"].ShouldBe(false);
    }

    [Fact]
    public void A_Configuration_Still_A_Clr_Object_Should_Have_The_Same_Names_As_One_Read_From_Storage()
    {
        var clr = new FieldDto
        {
            Id = Guid.NewGuid(),
            Name = "category",
            DisplayName = "Category",
            FieldTypeName = "Select",
            Configuration = new Dictionary<string, object?>
            {
                [SelectConfigurationNames.Options] = new List<SelectListItem> { new("Engineering", "engineering", false) }
            }
        };

        var option = ((ScriptArray)Configuration(clr)["Select.Options"]!)[0].ShouldBeOfType<ScriptObject>();

        option["Text"].ShouldBe("Engineering");
        option["Value"].ShouldBe("engineering");
    }

    private static ScriptObject Configuration(FieldDto field)
    {
        var definitions = new ScribanTestRenderer().ModelBuilder.BuildFieldDefinitions(new[] { field });
        return ((ScriptObject)definitions[field.Name]!)["configuration"].ShouldBeOfType<ScriptObject>();
    }

    [Fact]
    public void A_Page_With_Content_Types_Should_List_Them_Under_Content_Types()
    {
        var fieldId = Guid.NewGuid();
        var page = ScribanTestRenderer.Page();
        page.ContentTypes = new List<ContentTypeDto>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "post",
                DisplayName = "Post",
                Fields = new List<ContentTypeFieldDto> { new() { FieldId = fieldId, Required = true, ShowInList = true, Order = 1 } }
            }
        };

        var result = new ScribanTestRenderer().ModelBuilder.BuildPageWithContentTypes(page);

        result["display_name"].ShouldBe("About us");
        result["is_home_page"].ShouldBe(false);

        var contentType = ((ScriptArray)result["content_types"]!).ShouldHaveSingleItem().ShouldBeOfType<ScriptObject>();
        contentType["name"].ShouldBe("post");

        var usage = ((ScriptArray)contentType["fields"]!).ShouldHaveSingleItem().ShouldBeOfType<ScriptObject>();
        usage["field_id"].ShouldBe(fieldId.ToString());
        usage["required"].ShouldBe(true);
        usage["show_in_list"].ShouldBe(true);
        usage["order"].ShouldBe(1);
    }

    [Fact]
    public void A_Page_Without_Its_Content_Types_Loaded_Should_Have_An_Empty_List()
    {
        var page = ScribanTestRenderer.Page();
        page.ContentTypes = null;

        var result = new ScribanTestRenderer().ModelBuilder.BuildPageWithContentTypes(page);

        ((ScriptArray)result["content_types"]!).Count.ShouldBe(0);
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
