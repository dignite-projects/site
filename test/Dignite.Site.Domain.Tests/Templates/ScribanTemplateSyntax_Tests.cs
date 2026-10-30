using Shouldly;
using Xunit;

namespace Dignite.Site.Templates;

public class ScribanTemplateSyntax_Tests
{
    [Theory]
    [InlineData("")]
    [InlineData("<p>plain markup</p>")]
    [InlineData("<h1>{{ page.name }}</h1>{{ if x }}yes{{ else }}no{{ end }}")]
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

    [Theory]
    [InlineData("{{ site_head }}")]
    [InlineData("<head>{{ site_head \"T\" }}</head>")]
    [InlineData("<html><head>{{ site_head }}</head><body>{{ body }}</body></html>")]
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
