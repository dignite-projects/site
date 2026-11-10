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

    /// <summary>A working copy from before the rule, or a restored release, can hold a path that is a file and a folder.</summary>
    [Fact]
    public void Should_Report_A_File_Whose_Path_Is_Also_A_Folder()
    {
        var analysis = Analyze(
            new()
            {
                ["layouts/default"] = Layout,
                ["Products/Campus"] = "<p>product</p>",
                ["Products/Campus/Pricing"] = "<p>pricing</p>",
                ["Products/CampusTrial"] = "<p>trial</p>"
            },
            pages: new[] { ("campus", "Products/Campus"), ("pricing", "Products/Campus/Pricing"), ("trial", "Products/CampusTrial") });

        var issue = analysis.Issues.Single(i => i.Code == SiteThemeIssueCodes.PathIsFolder);
        issue.Severity.ShouldBe(SiteThemeIssueSeverity.Error);
        issue.Path.ShouldBe("Products/Campus");
        issue.Data["Other"].ShouldBe("Products/Campus/Pricing");
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

    /// <summary>The render fails with "$back_url was not found" wherever the partial is included without the argument.</summary>
    [Fact]
    public void Should_Report_An_Include_That_Leaves_Out_An_Argument_The_Partial_Reads()
    {
        var analysis = Analyze(new()
        {
            ["layouts/default"] = Layout,
            ["Events"] = "{{ include \"_header\" title: \"Events\" back_url: \"/\" }}\n{{ include \"_header\" title: \"More\" }}",
            ["_header"] = "<h1>{{ $title }}</h1>{{ if $back_url }}<a href=\"{{ $back_url }}\">back</a>{{ end }}"
        }, pages: ("events", "Events"));

        var issue = analysis.Issues.Single(i => i.Code == SiteThemeIssueCodes.IncludeArgumentMissing);
        issue.Severity.ShouldBe(SiteThemeIssueSeverity.Error);
        issue.Path.ShouldBe("Events");
        issue.Line.ShouldBe(2);
        issue.Data["Target"].ShouldBe("_header");
        issue.Data["Variable"].ShouldBe("back_url");
        analysis.HasErrors.ShouldBeTrue();
    }

    [Fact]
    public void Should_Report_Every_Argument_An_Include_Leaves_Out_And_Follow_A_Piped_Include()
    {
        var analysis = Analyze(new()
        {
            ["layouts/default"] = Layout,
            ["Events"] = "{{ \"_card\" | include a: 1 }}\n{{ \"_card\" | include }}\n{{ include \"_card\" }}",
            ["_card"] = "{{ $a }}{{ $b }}"
        }, pages: ("events", "Events"));

        analysis.Issues
            .Where(i => i.Code == SiteThemeIssueCodes.IncludeArgumentMissing)
            .Select(i => (i.Line!.Value, i.Data["Variable"]))
            .ShouldBe(new[] { (1, "b"), (2, "a"), (2, "b"), (3, "a"), (3, "b") }, ignoreOrder: true);
    }

    [Fact]
    public void Should_Accept_An_Include_That_Passes_What_The_Partial_Reads()
    {
        var analysis = Analyze(new()
        {
            ["layouts/default"] = Layout,
            // An optional argument is passed as null.
            ["Events"] = "{{ include \"_header\" title: page.display_name back_url: null }}",
            ["_header"] = "<h1>{{ $title }}</h1>{{ if $back_url }}<a href=\"{{ $back_url }}\">back</a>{{ end }}"
        }, pages: ("events", "Events"));

        analysis.Issues.ShouldBeEmpty();
    }

    /// <summary>A local the partial gives a value itself is not an argument; neither are the positional ones, the loop state and what a function reads.</summary>
    [Fact]
    public void Should_Not_Ask_For_Locals_The_Partial_Sets_Or_Which_Are_Not_Arguments()
    {
        var analysis = Analyze(new()
        {
            ["layouts/default"] = Layout,
            ["Events"] = "{{ include \"_list\" }}",
            ["_list"] =
                "{{ $count = 0 }}{{ capture $label }}items{{ end }}" +
                "{{ for $item in contents }}{{ $count = $count + 1 }}{{ $for.index }}{{ $item.slug }}{{ end }}" +
                "{{ $1 }}{{ $label }}{{ func shout }}{{ $inside }}{{ end }}"
        }, pages: ("events", "Events"));

        analysis.Issues.ShouldBeEmpty();
    }

    /// <summary>The include renders the version that fits the page's language, so each version must get what it reads.</summary>
    [Fact]
    public void Should_Hold_The_Language_Versions_Of_A_Partial_To_The_Same_Arguments()
    {
        var analysis = Analyze(new()
        {
            ["layouts/default"] = Layout,
            ["Events"] = "{{ include \"_card\" title: \"x\" }}",
            ["_card"] = "{{ $title }}",
            ["_card.ja"] = "{{ $title }}{{ $note }}"
        }, pages: ("events", "Events"));

        var issue = analysis.Issues.Single(i => i.Code == SiteThemeIssueCodes.IncludeArgumentMissing);
        issue.Data["Target"].ShouldBe("_card");
        issue.Data["Variable"].ShouldBe("note");
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

    /// <summary>`t` reads the theme's texts by language; nothing refers to the file by name.</summary>
    [Fact]
    public void Should_Not_Call_The_Themes_Texts_Unused()
    {
        var analysis = Analyze(new()
        {
            ["layouts/default"] = Layout,
            ["locales/en.json"] = "{ \"culture\": \"en\", \"texts\": { \"A\": \"a\" } }",
            ["locales/ja.json"] = "{ \"culture\": \"ja\", \"texts\": { \"A\": \"あ\" } }"
        });

        analysis.Issues.ShouldBeEmpty();
        analysis.Files["locales/ja.json"].TextFile!.CultureName.ShouldBe("ja");
        analysis.Files["layouts/default"].TextFile.ShouldBeNull();
    }

    [Fact]
    public void Should_Report_A_Text_File_That_Is_Not_Valid()
    {
        var analysis = Analyze(new()
        {
            ["layouts/default"] = Layout,
            ["locales/en.json"] = "{ \"culture\": \"en\", \"texts\": { \"A\": \"a\" } }",
            ["locales/ja.json"] = "{ not json",
            ["locales/fr.json"] = "{ \"culture\": \"en\", \"texts\": {} }"
        });

        var issues = analysis.Issues.Where(i => i.Code == SiteThemeIssueCodes.TextFileInvalid).ToList();

        issues.Select(i => i.Path).ShouldBe(new[] { "locales/fr.json", "locales/ja.json" });
        issues.ShouldAllBe(i => i.Severity == SiteThemeIssueSeverity.Error);
        issues[1].Data["Message"].ShouldContain("not valid JSON");
        analysis.HasErrors.ShouldBeTrue();
        Codes(analysis).ShouldNotContain(SiteThemeIssueCodes.UnusedFile);
    }

    [Fact]
    public void Should_Report_Two_Files_For_One_Language_Once_On_The_Later_One()
    {
        var analysis = Analyze(new()
        {
            ["layouts/default"] = Layout,
            ["locales/ja.json"] = "{ \"culture\": \"ja\", \"texts\": {} }",
            ["locales/JA.json"] = "{ \"culture\": \"JA\", \"texts\": {} }"
        });

        var issue = analysis.Issues.Single(i => i.Code == SiteThemeIssueCodes.TextFileDuplicate);

        issue.Path.ShouldBe("locales/ja.json");
        issue.Data["Other"].ShouldBe("locales/JA.json");
        issue.Data["Culture"].ShouldBe("ja");
    }

    /// <summary>Outside locales/ a .json file is a static file like any other.</summary>
    [Fact]
    public void Should_Treat_A_Json_File_Elsewhere_As_A_Static_File()
    {
        var analysis = Analyze(new() { ["layouts/default"] = Layout, ["assets/ja.json"] = "{ not json" });

        Codes(analysis).ShouldBe(new[] { SiteThemeIssueCodes.UnusedFile });
        analysis.Files["assets/ja.json"].TextFile.ShouldBeNull();
    }

    /// <summary>The version of a template for a language is used as the template is: nothing refers to it by name.</summary>
    [Fact]
    public void Should_Find_Nothing_Wrong_With_The_Language_Versions_Of_A_Healthy_Theme()
    {
        var analysis = Analyze(
            new()
            {
                ["layouts/default"] = Layout,
                ["layouts/default.ja"] = Layout,
                ["Events"] = "<h1>{{ page.display_name }}</h1>{{ include \"blog/_card\" }}",
                ["Events.ja"] = "<h1>{{ page.display_name }}</h1>{{ include \"blog/_card\" }}",
                ["blog/_card"] = "<div>{{ content.slug }}</div>",
                ["blog/_card.ja"] = "<div>{{ content.slug }}様</div>"
            },
            pages: new[] { ("events", "Events") });

        analysis.Issues.ShouldBeEmpty();
    }

    [Fact]
    public void Should_Not_Offer_A_Language_Version_As_A_Layout()
    {
        var analysis = Analyze(new() { ["layouts/default"] = Layout, ["layouts/default.ja"] = Layout });

        analysis.Layouts.ShouldBe(new[] { "layouts/default" });
    }

    [Fact]
    public void Should_Report_A_Language_Version_Without_Its_Template()
    {
        var analysis = Analyze(new() { ["layouts/default"] = Layout, ["Events.ja"] = "<p>x</p>", ["assets/site.css.ja"] = "" });

        var issue = analysis.Issues.Single(i => i.Code == SiteThemeIssueCodes.LanguageVersionWithoutTemplate);
        issue.Path.ShouldBe("Events.ja");
        issue.Severity.ShouldBe(SiteThemeIssueSeverity.Error);
        issue.Data["Template"].ShouldBe("Events");
        analysis.HasErrors.ShouldBeTrue();
    }

    /// <summary>A page in Japanese renders in the Japanese layout, so that one has to be a layout too.</summary>
    [Fact]
    public void Should_Hold_The_Language_Versions_Of_A_Layout_To_What_A_Layout_Must_Do()
    {
        var analysis = Analyze(new()
        {
            ["layouts/default"] = Layout,
            ["layouts/default.ja"] = "<body>{{ body }}</body>"
        });

        analysis.Issues.ShouldContain(i => i.Code == SiteThemeIssueCodes.LayoutWithoutSiteHead && i.Path == "layouts/default.ja");
    }

    /// <summary>A language version has its own front matter, and so may choose a layout of its own - which must exist.</summary>
    [Fact]
    public void Should_Check_The_Layout_A_Language_Version_Chooses()
    {
        var analysis = Analyze(
            new()
            {
                ["layouts/default"] = Layout,
                ["Events"] = "<p>x</p>",
                ["Events.ja"] = "+++\nlayout = \"layouts/missing\"\n+++\n<p>x</p>"
            },
            pages: new[] { ("events", "Events") });

        analysis.Issues.ShouldContain(i =>
            i.Code == SiteThemeIssueCodes.ReferenceMissing && i.Path == "Events.ja" && i.Data["Target"] == "layouts/missing");
    }

    [Fact]
    public void Should_Check_What_A_Language_Version_Includes()
    {
        var analysis = Analyze(
            new()
            {
                ["layouts/default"] = Layout,
                ["Events"] = "<p>x</p>",
                ["Events.ja"] = "{{ include \"blog/_gone\" }}"
            },
            pages: new[] { ("events", "Events") });

        analysis.Issues.ShouldContain(i =>
            i.Code == SiteThemeIssueCodes.ReferenceMissing && i.Path == "Events.ja" && i.Data["Target"] == "blog/_gone");
    }

    [Fact]
    public void Should_Report_An_Unused_Template_But_Not_Its_Language_Versions_Apart()
    {
        var analysis = Analyze(new() { ["layouts/default"] = Layout, ["_orphan"] = "", ["_orphan.ja"] = "" });

        analysis.Issues.Where(i => i.Code == SiteThemeIssueCodes.UnusedFile).Select(i => i.Path)
            .ShouldBe(new[] { "_orphan" });
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

    /// <summary>
    /// What a page rendered through a template runs: the template, its layout - chosen or the default - and what either
    /// includes, through any depth, with every language version of a layout or a partial. Not another page template, not
    /// a static file, not a partial nothing on the way includes.
    /// </summary>
    [Fact]
    public void Should_Tell_What_A_Page_Template_Renders()
    {
        var analysis = Analyze(
            new()
            {
                ["layouts/default"] = "<html><head>{{ site_head }}</head><body>{{ include \"_nav\" }}{{ body }}</body></html>",
                ["layouts/landing"] = Layout,
                ["_nav"] = "<nav>{{ include \"_logo\" }}</nav>",
                ["_nav.ja"] = "<nav>ja</nav>",
                ["_logo"] = "<img src=\"{{ asset_url \"img/logo.svg\" }}\">",
                ["Events"] = "{{ include \"blog/_card\" }}",
                ["Events.ja"] = "+++\nlayout = \"layouts/landing\"\n+++\n<p>ja</p>",
                ["Landing"] = "+++\nlayout = \"layouts/landing\"\n+++\n<p>hi</p>",
                ["blog/_card"] = "<div></div>",
                ["_unused"] = "<p></p>",
                ["img/logo.svg"] = "<svg/>"
            },
            pages: new[] { ("events", "Events"), ("landing", "Landing") });

        analysis.GetRenderedFiles("Events").ShouldBe(
            new[] { "Events", "layouts/default", "_nav", "_nav.ja", "_logo", "blog/_card" }, ignoreOrder: true);

        // A language version is held to its own layout and to its template's.
        analysis.GetRenderedFiles("Events.ja").ShouldBe(
            new[] { "Events.ja", "layouts/landing", "layouts/default", "_nav", "_nav.ja", "_logo" }, ignoreOrder: true);

        analysis.GetRenderedFiles("Landing").ShouldBe(new[] { "Landing", "layouts/landing" }, ignoreOrder: true);
    }

    [Fact]
    public void Should_Tell_What_A_Page_Template_Without_A_Layout_Renders()
    {
        var analysis = Analyze(
            new() { ["Bare"] = "+++\nlayout = \"none\"\n+++\n<html><head>{{ site_head }}</head></html>", ["layouts/default"] = Layout },
            pages: ("bare", "Bare"));

        analysis.GetRenderedFiles("Bare").ShouldBe(new[] { "Bare" });
    }
}
