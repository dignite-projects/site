using System.Linq;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Site.Themes;

public class SiteThemeHelpers_Tests
{
    [Fact]
    public void Fingerprint_Should_Not_Depend_On_File_Order()
    {
        var a = SiteThemeFingerprint.Compute(new[] { ("a", "1"), ("b", "2") }, "layouts/default");
        var b = SiteThemeFingerprint.Compute(new[] { ("b", "2"), ("a", "1") }, "layouts/default");

        a.ShouldBe(b);
    }

    [Fact]
    public void Fingerprint_Should_Change_With_A_File_Or_The_Default_Layout()
    {
        var baseline = SiteThemeFingerprint.Compute(new[] { ("a", "1") }, "layouts/default");

        SiteThemeFingerprint.Compute(new[] { ("a", "2") }, "layouts/default").ShouldNotBe(baseline);
        SiteThemeFingerprint.Compute(new[] { ("b", "1") }, "layouts/default").ShouldNotBe(baseline);
        SiteThemeFingerprint.Compute(new[] { ("a", "1"), ("b", "1") }, "layouts/default").ShouldNotBe(baseline);
        SiteThemeFingerprint.Compute(new[] { ("a", "1") }, null).ShouldNotBe(baseline);
    }

    [Fact]
    public void Changes_Should_Classify_Every_Path()
    {
        var changes = SiteThemeChanges.Compare(
            new[] { ("same", "1"), ("changed", "2"), ("new", "3") },
            new[] { ("same", "1"), ("changed", "x"), ("gone", "4") });

        changes.ToDictionary(c => c.Path, c => c.Status).ShouldBe(new System.Collections.Generic.Dictionary<string, SiteThemeFileStatus>
        {
            ["changed"] = SiteThemeFileStatus.Modified,
            ["gone"] = SiteThemeFileStatus.Deleted,
            ["new"] = SiteThemeFileStatus.Added,
            ["same"] = SiteThemeFileStatus.Unchanged
        });
        changes.Select(c => c.Path).ShouldBe(new[] { "changed", "gone", "new", "same" });
    }

    [Fact]
    public void Rewriter_Should_Replace_Every_Reference_In_Its_Own_Quotes()
    {
        const string body = "+++\nlayout = \"_old\"\n+++\n{{ include '_old' }}{{ include \"_other\" }}{{ \"_old\" | include }}";
        var template = ScribanTemplateSyntax.Parse(body);
        var references = ScribanTemplateSyntax.GetReferences(template)
            .Append(ScribanTemplateSyntax.ReadFrontMatter(template).Layout!)
            .Where(r => r.Path == "_old");

        SiteThemeReferenceRewriter.Rewrite(body, references, "shared/_new").ShouldBe(
            "+++\nlayout = \"shared/_new\"\n+++\n{{ include 'shared/_new' }}{{ include \"_other\" }}{{ \"shared/_new\" | include }}");
    }

    [Fact]
    public void Skeleton_Should_Be_A_Working_Theme()
    {
        var analysis = SiteThemeAnalyzer.Analyze(
            SiteThemeSkeleton.Files.Select(file => new SiteThemeSourceFile(file.Path, file.Body)),
            SiteThemeConsts.InitialDefaultLayout,
            Enumerable.Empty<SiteThemePageTemplate>());

        analysis.Issues.ShouldBeEmpty();
    }

    [Fact]
    public void Theme_Should_Start_With_The_Initial_Default_Layout()
    {
        new SiteTheme(System.Guid.NewGuid()).DefaultLayout.ShouldBe(SiteThemeConsts.InitialDefaultLayout);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(" /layouts/landing ", "layouts/landing")]
    public void Theme_Should_Normalize_Its_Default_Layout(string? value, string? expected)
    {
        var theme = new SiteTheme(System.Guid.NewGuid());

        theme.SetDefaultLayout(value);

        theme.DefaultLayout.ShouldBe(expected);
    }

    [Theory]
    [InlineData("assets/site.css")]
    [InlineData("a//b")]
    public void Theme_Should_Refuse_A_Default_Layout_That_Is_Not_A_Template_Path(string value)
    {
        var theme = new SiteTheme(System.Guid.NewGuid());

        Should.Throw<BusinessException>(() => theme.SetDefaultLayout(value)).Code
            .ShouldBe(SiteErrorCodes.SiteThemeDefaultLayoutInvalid);
    }

    [Fact]
    public void File_Should_Keep_Its_Hash_With_Its_Body()
    {
        var file = new SiteThemeFile(System.Guid.NewGuid(), System.Guid.NewGuid(), "/Events", "a");

        file.Path.ShouldBe("Events");
        file.ContentHash.ShouldBe(SiteThemePaths.ComputeContentHash("a"));

        file.SetBody("b");
        file.ContentHash.ShouldBe(SiteThemePaths.ComputeContentHash("b"));
    }

    [Fact]
    public void File_Should_Refuse_An_Extension_No_Static_File_Has()
    {
        Should.Throw<BusinessException>(() => new SiteThemeFile(System.Guid.NewGuid(), System.Guid.NewGuid(), "post.html", ""))
            .Code.ShouldBe(SiteErrorCodes.SiteThemeFileExtensionNotAllowed);
    }

    [Fact]
    public void File_Should_Refuse_A_Malformed_Path()
    {
        Should.Throw<InvalidValueFormatException>(() => new SiteThemeFile(System.Guid.NewGuid(), System.Guid.NewGuid(), "../x", ""));
    }
}
