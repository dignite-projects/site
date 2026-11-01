using System.Linq;
using Shouldly;
using Xunit;

namespace Dignite.Site.Themes;

public class ScribanTemplateSyntax_Tests
{
    [Theory]
    [InlineData("")]
    [InlineData("<p>plain markup</p>")]
    [InlineData("<h1>{{ page.name }}</h1>{{ if x }}yes{{ else }}no{{ end }}")]
    [InlineData("+++\nlayout = \"layouts/landing\"\n+++\n<p>{{ x }}</p>")]
    [InlineData("+++\r\nlayout = \"none\"\r\n+++\r\n<p>{{ x }}</p>")]
    public void Should_Find_No_Errors_In_A_Valid_Template(string body)
    {
        ScribanTemplateSyntax.GetErrors(body).ShouldBeEmpty();
    }

    [Fact]
    public void Should_Report_An_Unterminated_Block()
    {
        var errors = ScribanTemplateSyntax.GetErrors("{{ if x }}no end");

        errors.Count.ShouldBeGreaterThan(0);
        errors[0].Line.ShouldBe(1);
        errors[0].Column.ShouldBeGreaterThan(0);
        errors[0].Message.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>Scriban counts from zero; what an editor shows, and what we report, counts from one.</summary>
    [Fact]
    public void Should_Report_One_Based_Positions()
    {
        var errors = ScribanTemplateSyntax.GetErrors("<p>first line</p>\n{{ if x }}no end");

        errors.Count.ShouldBeGreaterThan(0);
        errors[0].Line.ShouldBe(2);
        errors[0].Column.ShouldBeGreaterThan(0);
    }

    /// <summary>An error after a front matter is reported at its line in the whole file, front matter included.</summary>
    [Fact]
    public void Should_Report_Positions_In_The_Whole_File_After_A_Front_Matter()
    {
        var errors = ScribanTemplateSyntax.GetErrors("+++\nlayout = \"x\"\n+++\n{{ if }}");

        errors.Count.ShouldBeGreaterThan(0);
        errors[0].Line.ShouldBe(4);
    }

    [Fact]
    public void Should_Report_An_Unclosed_Front_Matter()
    {
        ScribanTemplateSyntax.GetErrors("+++\nlayout = \"x\"\n<p>hi</p>").ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("+++\nlayout = \"x\"\n+++\n", true)]
    [InlineData("+++\r\nlayout = \"x\"\r\n+++\r\n", true)]
    [InlineData("+++", true)]
    [InlineData("\n+++\nlayout = \"x\"\n+++\n", false)]
    [InlineData("+++ not front matter", false)]
    [InlineData("<p>a</p>\n+++\n", false)]
    [InlineData("", false)]
    public void Should_Recognize_Front_Matter_Only_On_The_First_Line(string body, bool expected)
    {
        ScribanTemplateSyntax.HasFrontMatter(body).ShouldBe(expected);
    }

    /// <summary>A template that does not start with a front matter is an ordinary template, whatever comes later.</summary>
    [Fact]
    public void Should_Parse_A_Template_Without_Front_Matter_As_Usual()
    {
        var template = ScribanTemplateSyntax.Parse("<p>a</p>\n+++\nlayout = \"x\"\n+++\n");

        template.HasErrors.ShouldBeFalse();
        ScribanTemplateSyntax.ReadFrontMatter(template).Exists.ShouldBeFalse();
    }

    [Fact]
    public void Should_Keep_The_Front_Matter_Out_Of_The_Output()
    {
        var template = ScribanTemplateSyntax.Parse("+++\nlayout = \"layouts/landing\"\n+++\n<p>{{ 1 + 1 }}</p>");

        template.Render().ShouldBe("<p>2</p>");
    }

    [Fact]
    public void Should_Read_The_Layout_Of_A_Front_Matter()
    {
        var frontMatter = ScribanTemplateSyntax.ReadFrontMatter(
            ScribanTemplateSyntax.Parse("+++\nlayout = \"/layouts/landing \"\n+++\n<p>x</p>"));

        frontMatter.Exists.ShouldBeTrue();
        frontMatter.Errors.ShouldBeEmpty();
        frontMatter.Layout.ShouldNotBeNull();
        frontMatter.Layout.FunctionName.ShouldBe("layout");
        frontMatter.Layout.Path.ShouldBe("layouts/landing");
        frontMatter.Layout.Line.ShouldBe(2);
    }

    [Fact]
    public void Should_Read_Layout_None()
    {
        var frontMatter = ScribanTemplateSyntax.ReadFrontMatter(ScribanTemplateSyntax.Parse("+++\nlayout = \"none\"\n+++\n"));

        frontMatter.Layout!.Path.ShouldBe(SiteThemeConsts.NoLayout);
    }

    [Fact]
    public void Should_Read_An_Empty_Front_Matter()
    {
        var frontMatter = ScribanTemplateSyntax.ReadFrontMatter(ScribanTemplateSyntax.Parse("+++\n+++\n<p>x</p>"));

        frontMatter.Exists.ShouldBeTrue();
        frontMatter.Layout.ShouldBeNull();
        frontMatter.Errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("+++\nlayout = \"a\" + \"b\"\n+++\n")]
    [InlineData("+++\nlayout = name\n+++\n")]
    [InlineData("+++\ntitle = \"About\"\n+++\n")]
    [InlineData("+++\nlayout = 1\n+++\n")]
    [InlineData("+++\nlayout = \"a\"\nlayout = \"b\"\n+++\n")]
    public void Should_Refuse_Anything_But_A_Literal_Layout(string body)
    {
        var frontMatter = ScribanTemplateSyntax.ReadFrontMatter(ScribanTemplateSyntax.Parse(body));

        frontMatter.Errors.ShouldNotBeEmpty();
        frontMatter.Errors[0].Line.ShouldBe(body.Contains("layout = \"a\"\nlayout") ? 3 : 2);
    }

    [Theory]
    [InlineData("{{ site_head }}")]
    [InlineData("<head>{{ site_head \"T\" }}</head>")]
    [InlineData("+++\nlayout = \"none\"\n+++\n<html><head>{{ site_head }}</head><body>{{ body }}</body></html>")]
    public void Should_Detect_A_Call_To_Site_Head(string body)
    {
        ScribanTemplateSyntax.CallsSiteHead(ScribanTemplateSyntax.Parse(body)).ShouldBeTrue();
    }

    /// <summary>
    /// The check walks the syntax tree, so the word in markup or in a string - which a text search would
    /// accept - does not count as a call.
    /// </summary>
    [Theory]
    [InlineData("{{ x }}")]
    [InlineData("")]
    [InlineData("<p>site_head</p>")]
    [InlineData("<!-- call site_head here -->{{ body }}")]
    [InlineData("{{ \"site_head\" }}")]
    public void Should_Not_Detect_Site_Head_Outside_A_Call(string body)
    {
        ScribanTemplateSyntax.CallsSiteHead(ScribanTemplateSyntax.Parse(body)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("{{ body }}", true)]
    [InlineData("<main>{{ body }}</main>", true)]
    [InlineData("<p>body</p>", false)]
    public void Should_Detect_A_Use_Of_Body(string body, bool expected)
    {
        ScribanTemplateSyntax.UsesBody(ScribanTemplateSyntax.Parse(body)).ShouldBe(expected);
    }

    [Fact]
    public void Should_Find_Literal_References()
    {
        var body = "{{ include \"blog/_sidebar\" }}\n<link href=\"{{ asset_url 'assets/site.css' }}\" />\n{{ \"_card\" | include }}";
        var references = ScribanTemplateSyntax.GetReferences(ScribanTemplateSyntax.Parse(body));

        references.Select(r => (r.FunctionName, r.Path)).ShouldBe(new[]
        {
            ("include", "blog/_sidebar"),
            ("asset_url", "assets/site.css"),
            ("include", "_card")
        });
    }

    [Fact]
    public void Should_Not_Find_Computed_Or_Textual_References()
    {
        var body = "{{ include (\"blog/\" + name) }}<p>include \"x\"</p>{{ \"include\" }}";

        ScribanTemplateSyntax.GetReferences(ScribanTemplateSyntax.Parse(body)).ShouldBeEmpty();
    }

    /// <summary>The offsets cover the literal with its quotes, so a rename can replace it in place.</summary>
    [Theory]
    [InlineData("{{ include \"_card\" }}", "\"_card\"")]
    [InlineData("{{ include '_card' }}", "'_card'")]
    [InlineData("+++\nlayout = \"x\"\n+++\n<p>{{ include \"_card\" }}</p>", "\"_card\"")]
    public void Should_Locate_A_Reference_In_The_Source(string body, string literal)
    {
        var reference = ScribanTemplateSyntax.GetReferences(ScribanTemplateSyntax.Parse(body)).Single();

        body.Substring(reference.Start, reference.Length).ShouldBe(literal);
    }

    [Fact]
    public void Should_Locate_The_Front_Matter_Layout_In_The_Source()
    {
        const string body = "+++\nlayout = \"layouts/landing\"\n+++\n<p>x</p>";
        var layout = ScribanTemplateSyntax.ReadFrontMatter(ScribanTemplateSyntax.Parse(body)).Layout!;

        body.Substring(layout.Start, layout.Length).ShouldBe("\"layouts/landing\"");
    }

    [Fact]
    public void Should_Format_One_Error_Per_Line()
    {
        var errors = new[]
        {
            new ScribanTemplateError(1, 2, "first"),
            new ScribanTemplateError(3, 4, "second")
        };

        ScribanTemplateSyntax.FormatErrors(errors).ShouldBe("(1,2): first\n(3,4): second");
        ScribanTemplateSyntax.FormatErrors(new ScribanTemplateError[0]).ShouldBe(string.Empty);
    }
}
