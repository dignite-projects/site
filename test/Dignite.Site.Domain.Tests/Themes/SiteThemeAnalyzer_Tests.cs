using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace Dignite.Site.Themes;

public class SiteThemeAnalyzer_Tests
{
    private const string Layout = "<html><head>{{ site_head }}</head><body>{{ body }}</body></html>";

    private static SiteThemeAnalysis Analyze(
        Dictionary<string, string> files,
        string? defaultLayout = "layouts/default",
        params (string Page, string Template)[] pages)
    {
        return SiteThemeAnalyzer.Analyze(
            files.Select(file => new SiteThemeSourceFile(file.Key, file.Value)),
            defaultLayout,
            pages.Select(page => new SiteThemePageTemplate(page.Page, page.Template)));
    }

    private static IEnumerable<string> Codes(SiteThemeAnalysis analysis, SiteThemeIssueSeverity? severity = null)
    {
        return analysis.Issues.Where(i => severity == null || i.Severity == severity).Select(i => i.Code);
    }

    [Fact]
    public void Should_Find_Nothing_Wrong_With_A_Healthy_Theme()
    {
        var analysis = Analyze(
            new()
            {
                ["layouts/default"] = "<html><head>{{ site_head }}<link href=\"{{ asset_url \"assets/site.css\" }}\"></head><body>{{ body }}</body></html>",
                ["layouts/landing"] = Layout,
                ["Events"] = "<h1>{{ page.display_name }}</h1>{{ include \"blog/_card\" }}",
                ["Landing"] = "+++\nlayout = \"layouts/landing\"\n+++\n<p>hi</p>",
                ["blog/_card"] = "<div>{{ content.slug }}</div>",
                ["assets/site.css"] = "body { color: red }"
            },
            pages: new[] { ("events", "Events"), ("landing", "Landing") });

        analysis.Issues.ShouldBeEmpty();
        analysis.HasErrors.ShouldBeFalse();
    }

    /// <summary>A layout is told by what it does - body and site_head, no front matter - not by where it lives or who uses it.</summary>
    [Fact]
    public void Should_List_The_Templates_That_Can_Be_A_Layout()
    {
        var analysis = Analyze(
            new()
            {
                ["layouts/default"] = Layout,
                // Not used by anything yet, and not under layouts/: still a layout.
                ["shell"] = Layout,
                // site_head comes through an include, body is written here.
                ["layouts/split"] = "<html><head>{{ include \"_head\" }}</head><body>{{ body }}</body></html>",
                ["_head"] = "{{ site_head }}",
                // Each of these is missing one thing.
                ["no-body"] = "<html><head>{{ site_head }}</head></html>",
                ["no-head"] = "<html><body>{{ body }}</body></html>",
                ["front-matter"] = "+++\nlayout = \"layouts/default\"\n+++\n" + Layout,
                ["broken"] = "{{ if }}" + Layout,
                ["Events"] = "<p>a page</p>",
                ["assets/site.css"] = "body { color: red }"
            },
            defaultLayout: null);

        analysis.Layouts.ShouldBe(new[] { "layouts/default", "layouts/split", "shell" });
    }

    [Fact]
    public void Should_Report_A_Syntax_Error_At_Its_Position()
    {
        var analysis = Analyze(new() { ["layouts/default"] = Layout, ["Events"] = "<p>\n{{ if x }}" }, pages: ("events", "Events"));

        var issue = analysis.Issues.Single(i => i.Code == SiteThemeIssueCodes.SyntaxError);
        issue.Severity.ShouldBe(SiteThemeIssueSeverity.Error);
        issue.Path.ShouldBe("Events");
        issue.Line.ShouldBe(2);
        issue.Data["Message"].ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>A static file's body is not Scriban - CSS with braces is not an error.</summary>
    [Fact]
    public void Should_Not_Parse_A_Static_File()
    {
        var analysis = Analyze(
            new() { ["layouts/default"] = Layout + "{{ asset_url \"assets/a.css\" }}", ["assets/a.css"] = "a{{color:red}}" });

        analysis.Issues.ShouldBeEmpty();
    }

    [Fact]
    public void Should_Report_An_Invalid_Front_Matter()
    {
        var analysis = Analyze(
            new() { ["layouts/default"] = Layout, ["Events"] = "+++\ntitle = \"x\"\n+++\n{{ site_head }}" },
            pages: ("events", "Events"));

        var issue = analysis.Issues.Single(i => i.Code == SiteThemeIssueCodes.FrontMatterInvalid);
        issue.Path.ShouldBe("Events");
        issue.Line.ShouldBe(2);
    }

    [Theory]
    [InlineData("layouts/missing")]
    [InlineData("assets/site.css")]
    public void Should_Report_A_Default_Layout_That_Is_Not_A_Template(string defaultLayout)
    {
        var analysis = Analyze(new() { ["assets/site.css"] = "" }, defaultLayout);

        var issue = analysis.Issues.Single(i => i.Code == SiteThemeIssueCodes.DefaultLayoutMissing);
        issue.Path.ShouldBeNull();
        issue.Data["Layout"].ShouldBe(defaultLayout);
    }

    [Fact]
    public void Should_Report_References_To_Missing_Files()
    {
        var analysis = Analyze(new()
        {
            ["layouts/default"] = Layout,
            ["Events"] = "+++\nlayout = \"layouts/nope\"\n+++\n{{ include \"_nope\" }}\n{{ asset_url \"assets/nope.css\" }}"
        }, pages: ("events", "Events"));

        var missing = analysis.Issues.Where(i => i.Code == SiteThemeIssueCodes.ReferenceMissing).ToList();
        missing.Select(i => (i.Data["Function"], i.Data["Target"])).ShouldBe(
            new[] { ("layout", "layouts/nope"), ("include", "_nope"), ("asset_url", "assets/nope.css") }, ignoreOrder: true);
        missing.Single(i => i.Data["Target"] == "_nope").Line.ShouldBe(4);
    }

    [Fact]
    public void Should_Report_References_To_The_Wrong_Kind_Of_File()
    {
        var analysis = Analyze(new()
        {
            ["layouts/default"] = Layout,
            ["Events"] = "+++\nlayout = \"assets/site.css\"\n+++\n{{ include \"assets/site.css\" }}{{ asset_url \"_card\" }}",
            ["_card"] = "",
            ["assets/site.css"] = ""
        }, pages: ("events", "Events"));

        analysis.Issues.Count(i => i.Code == SiteThemeIssueCodes.ReferenceToStaticFile).ShouldBe(2);
        analysis.Issues.Single(i => i.Code == SiteThemeIssueCodes.AssetUrlToTemplate).Data["Target"].ShouldBe("_card");
    }

    [Fact]
    public void Should_Report_An_Include_Cycle_Once()
    {
        var analysis = Analyze(new()
        {
            ["layouts/default"] = Layout,
            ["_a"] = "{{ include \"_b\" }}",
            ["_b"] = "{{ include \"_a\" }}",
            ["_self"] = "{{ include \"_self\" }}"
        });

        var cycles = analysis.Issues.Where(i => i.Code == SiteThemeIssueCodes.IncludeCycle).ToList();
        cycles.Count.ShouldBe(2);
        cycles.Select(i => i.Data["Chain"]).ShouldContain("_a -> _b -> _a");
        cycles.Select(i => i.Data["Chain"]).ShouldContain("_self -> _self");
    }

    [Fact]
    public void Should_Require_Site_Head_In_A_Layout()
    {
        var analysis = Analyze(new() { ["layouts/default"] = "<body>{{ body }}</body>" });

        var issue = analysis.Issues.Single(i => i.Code == SiteThemeIssueCodes.LayoutWithoutSiteHead);
        issue.Path.ShouldBe("layouts/default");
        issue.Severity.ShouldBe(SiteThemeIssueSeverity.Error);
    }

    /// <summary>A layout may call site_head through a partial it includes.</summary>
    [Fact]
    public void Should_Follow_Includes_Looking_For_Site_Head()
    {
        var analysis = Analyze(new()
        {
            ["layouts/default"] = "<html>{{ include \"_head\" }}<body>{{ include \"_main\" }}</body></html>",
            ["_head"] = "<head>{{ include \"_meta\" }}</head>",
            ["_meta"] = "{{ site_head }}",
            ["_main"] = "{{ body }}"
        });

        analysis.Issues.ShouldBeEmpty();
    }

    [Fact]
    public void Should_Warn_About_A_Layout_That_Never_Writes_The_Body()
    {
        var analysis = Analyze(new() { ["layouts/default"] = "<head>{{ site_head }}</head>" });

        var issue = analysis.Issues.Single(i => i.Code == SiteThemeIssueCodes.LayoutWithoutBody);
        issue.Severity.ShouldBe(SiteThemeIssueSeverity.Warning);
    }

    [Fact]
    public void Should_Require_Site_Head_In_A_Page_Without_A_Layout()
    {
        var analysis = Analyze(
            new()
            {
                ["layouts/default"] = Layout,
                ["Feed"] = "+++\nlayout = \"none\"\n+++\n<rss></rss>",
                ["Plain"] = "+++\nlayout = \"none\"\n+++\n<html><head>{{ site_head }}</head></html>"
            },
            pages: new[] { ("feed", "Feed"), ("plain", "Plain") });

        analysis.Issues.Where(i => i.Code == SiteThemeIssueCodes.PageWithoutSiteHead).Select(i => i.Path).ShouldBe(new[] { "Feed" });
    }

    [Fact]
    public void Should_Render_A_Page_Without_A_Layout_When_There_Is_No_Default()
    {
        var analysis = Analyze(new() { ["Events"] = "<p>x</p>" }, defaultLayout: null, ("events", "Events"));

        analysis.Issues.Select(i => i.Code).ShouldBe(new[] { SiteThemeIssueCodes.PageWithoutSiteHead });
    }

    [Fact]
    public void Should_Check_The_Layout_A_Front_Matter_Chooses_Instead_Of_The_Default()
    {
        var analysis = Analyze(
            new()
            {
                ["layouts/default"] = Layout,
                ["layouts/bare"] = "{{ body }}",
                ["Landing"] = "+++\nlayout = \"layouts/bare\"\n+++\n<p>x</p>"
            },
            pages: ("landing", "Landing"));

        analysis.Issues.Single(i => i.Code == SiteThemeIssueCodes.LayoutWithoutSiteHead).Path.ShouldBe("layouts/bare");
    }

    [Fact]
    public void Should_Refuse_Front_Matter_In_A_Layout()
    {
        var analysis = Analyze(new() { ["layouts/default"] = "+++\nlayout = \"none\"\n+++\n" + Layout });

        analysis.Issues.Single(i => i.Code == SiteThemeIssueCodes.FrontMatterInLayout).Path.ShouldBe("layouts/default");
    }

    [Fact]
    public void Should_Warn_About_Pages_Rendered_By_Razor()
    {
        var analysis = Analyze(
            new() { ["layouts/default"] = Layout },
            pages: new[] { ("home", "Default"), ("about", "Default"), ("blog", "/Default") });

        var issue = analysis.Issues.Single(i => i.Code == SiteThemeIssueCodes.PageRenderedByRazor);
        issue.Severity.ShouldBe(SiteThemeIssueSeverity.Warning);
        issue.Data["Template"].ShouldBe("Default");
        issue.Data["Pages"].ShouldBe("about, blog, home");
    }

    [Fact]
    public void Should_Warn_About_A_File_Nothing_Uses()
    {
        var analysis = Analyze(new() { ["layouts/default"] = Layout, ["_orphan"] = "", ["assets/old.css"] = "" });

        analysis.Issues.Where(i => i.Code == SiteThemeIssueCodes.UnusedFile).Select(i => i.Path)
            .ShouldBe(new[] { "_orphan", "assets/old.css" });
        analysis.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void Should_Warn_About_A_Layout_Used_Another_Way_Too()
    {
        var analysis = Analyze(
            new() { ["layouts/default"] = Layout, ["Events"] = "{{ include \"layouts/default\" }}" },
            pages: new[] { ("events", "Events"), ("odd", "layouts/default") });

        var issue = analysis.Issues.Single(i => i.Code == SiteThemeIssueCodes.MixedRoles);
        issue.Path.ShouldBe("layouts/default");
        issue.Data["Roles"].ShouldBe("page, include");
    }

    [Fact]
    public void Should_List_Errors_Before_Warnings()
    {
        var analysis = Analyze(new() { ["_orphan"] = "", ["layouts/default"] = "<p>{{ body }}</p>" });

        analysis.Issues.Select(i => i.Severity).ShouldBe(new[] { SiteThemeIssueSeverity.Error, SiteThemeIssueSeverity.Warning });
    }

    [Fact]
    public void Should_Tell_Who_Uses_A_File()
    {
        var analysis = Analyze(
            new()
            {
                ["layouts/default"] = Layout,
                ["layouts/landing"] = Layout + "{{ include \"_card\" }}",
                ["Events"] = "{{ include \"_card\" }}{{ asset_url \"assets/site.css\" }}{{ include \"_card\" }}",
                ["Landing"] = "+++\nlayout = \"layouts/landing\"\n+++\n",
                ["_card"] = "",
                ["assets/site.css"] = ""
            },
            pages: new[] { ("events", "Events"), ("landing", "Landing") });

        analysis.GetUsages("_card").Select(u => (u.Kind, u.SourcePath)).ShouldBe(
            new[] { (SiteThemeUsageKind.Include, "layouts/landing"), (SiteThemeUsageKind.Include, "Events") }, ignoreOrder: true);
        analysis.GetUsages("Events").Single().ShouldBe(new SiteThemeUsage(SiteThemeUsageKind.Page, null, "events"));
        analysis.GetUsages("layouts/landing").Single().ShouldBe(new SiteThemeUsage(SiteThemeUsageKind.Layout, "Landing", null));
        analysis.GetUsages("layouts/default").Single().Kind.ShouldBe(SiteThemeUsageKind.DefaultLayout);
        analysis.GetUsages("assets/site.css").Single().Kind.ShouldBe(SiteThemeUsageKind.AssetUrl);
        analysis.GetUsages("nothing").ShouldBeEmpty();
    }
}
