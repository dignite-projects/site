using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Dignite.Abp.FlexFields;
using Dignite.Abp.FlexFields.Select;
using Dignite.Site.Contents;
using Dignite.Site.ContentTypes;
using Dignite.Site.Public.Routing;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Templating;

public class ContentTemplateExtensions_Tests
{
    [Fact]
    public void GetText_Reads_Json_Scalars_As_Written()
    {
        var content = ContentFromJson("""{"title":"Hello","year":2025,"featured":true,"none":null}""");

        content.GetText("title").ShouldBe("Hello");
        content.GetText("year").ShouldBe("2025");
        content.GetText("featured").ShouldBe("true");
        content.GetText("none").ShouldBeNull();
        content.GetText("missing").ShouldBeNull();
    }

    [Fact]
    public void GetText_Reads_Plain_Clr_Values()
    {
        var content = new ContentDto
        {
            FieldValues = new Dictionary<string, object?>
            {
                ["title"] = "Hello",
                ["year"] = 2025,
                ["featured"] = true,
                ["tags"] = new List<string> { "a", "b" }
            }
        };

        content.GetText("title").ShouldBe("Hello");
        content.GetText("year").ShouldBe("2025");
        content.GetText("featured").ShouldBe("true");
        content.GetText("tags").ShouldBe("a");
        content.GetTexts("tags").ShouldBe(new[] { "a", "b" });
    }

    [Fact]
    public void GetTexts_Returns_Every_Value_In_Stored_Order()
    {
        var content = ContentFromJson("""{"category":["media","automotive"],"single":"design"}""");

        content.GetTexts("category").ShouldBe(new[] { "media", "automotive" });
        content.GetText("category").ShouldBe("media");
        content.GetTexts("single").ShouldBe(new[] { "design" });
        content.GetTexts("missing").ShouldBeEmpty();
    }

    [Fact]
    public void GetSelectedOptions_Takes_Labels_From_The_Field_Definition()
    {
        var item = RenderItem(
            """{"category":["media","gone","automotive"]}""",
            SelectField("category", ("automotive", "汽车"), ("media", "媒体")));

        var selected = item.GetSelectedOptions("category");

        selected.Select(o => o.Value).ShouldBe(new[] { "media", "gone", "automotive" });
        // "gone" is no longer an option: its value stands in for the label.
        selected.Select(o => o.Text).ShouldBe(new[] { "媒体", "gone", "汽车" });
    }

    [Fact]
    public void GetSelectedOptions_Is_Empty_Without_A_Value()
    {
        var item = RenderItem("""{}""", SelectField("category", ("automotive", "汽车")));

        item.GetSelectedOptions("category").ShouldBeEmpty();
    }

    [Fact]
    public void GetSelectedOptions_Falls_Back_To_Values_Without_A_Definition()
    {
        var item = RenderItem("""{"category":"design"}""");

        item.GetSelectedOptions("category").Single().Text.ShouldBe("design");
    }

    [Fact]
    public void Matrix_Blocks_Expose_Their_Sub_Field_Values()
    {
        var content = ContentFromJson("""
            {"team_members":[
              {"blockTypeName":"member","values":{"name":"Du","position":"CEO","avatar":[{"id":"1","url":"https://x/a.png"}]}},
              {"blockTypeName":"member","values":{"name":"Li"}},
              "not a block"
            ]}
            """);

        var blocks = content.GetMatrixBlocks("team_members");

        blocks.Count.ShouldBe(2);
        blocks[0].GetText("name").ShouldBe("Du");
        blocks[0].GetText("position").ShouldBe("CEO");
        blocks[0].GetFileUrl("avatar").ShouldBe("https://x/a.png");
        blocks[1].GetText("position").ShouldBeNull();
        blocks[1].GetFileUrl("avatar").ShouldBeNull();
        content.GetMatrixBlocks("missing").ShouldBeEmpty();
    }

    [Fact]
    public void GetFileUrl_Reads_The_First_File()
    {
        var content = ContentFromJson("""{"photo":[{"url":"https://x/1.png"},{"url":"https://x/2.png"}],"empty":[]}""");

        content.GetFileUrl("photo").ShouldBe("https://x/1.png");
        content.GetFileUrl("empty").ShouldBeNull();
        content.GetFileUrl("missing").ShouldBeNull();
    }

    [Fact]
    public void Render_Item_Reads_Through_To_Its_Content()
    {
        var item = RenderItem("""{"title":"Hello","tags":["a","b"]}""");

        item.GetText("title").ShouldBe("Hello");
        item.GetTexts("tags").ShouldBe(new[] { "a", "b" });
    }

    private static ContentDto ContentFromJson(string json)
    {
        // What a host calling Site over HTTP receives: every value a JsonElement.
        using var document = JsonDocument.Parse(json);
        return new ContentDto
        {
            FieldValues = document.RootElement.EnumerateObject()
                .ToDictionary(p => p.Name, p => (object?)p.Value.Clone())
        };
    }

    private static ContentRenderViewModel RenderItem(string json, params FlexFieldValue[] fields)
    {
        return new ContentRenderViewModel
        {
            Content = ContentFromJson(json),
            ContentType = new ContentTypeDto(),
            Fields = fields
        };
    }

    private static FlexFieldValue SelectField(string name, params (string Value, string Text)[] options)
    {
        var configuration = new SelectConfiguration
        {
            Multiple = true,
            Options = options.Select(o => new SelectListItem(o.Text, o.Value, false)).ToList()
        };

        return new FlexFieldValue(new FlexFieldData
        {
            Id = Guid.NewGuid(),
            Name = name,
            DisplayName = name,
            FieldTypeName = SelectFieldType.ControlName,
            Configuration = configuration.ConfigurationDictionary
        });
    }
}
