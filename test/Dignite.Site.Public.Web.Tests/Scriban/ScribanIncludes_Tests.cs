using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Templates;
using global::Scriban.Syntax;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// <c>include</c>: a page template or the layout pulls in the tenant's published partials - and nothing else -
/// rendered with the same encoding rules as the template around them.
/// </summary>
public class ScribanIncludes_Tests
{
    /// <summary>A layout that only adds the head tags, for tests about the page template itself.</summary>
    private const string HeadOnlyLayout = "{{ site_head }}{{ body }}";

    [Fact]
    public async Task A_Published_Partial_Should_Render_In_Place()
    {
        var html = await new ScribanTestRenderer().RenderAsync(
            "<main>{{ include \"partials/card\" }}</main>",
            HeadOnlyLayout,
            new Dictionary<string, string> { ["partials/card"] = "<div class=\"card\">card</div>" });

        html.ShouldEndWith("<main><div class=\"card\">card</div></main>");
    }

    /// <summary>
    /// The partial's markup is written as it is, and a value it writes is encoded exactly once - not a second
    /// time when its output reaches the page.
    /// </summary>
    [Fact]
    public async Task A_Partials_Markup_Should_Stay_Markup_And_Its_Values_Be_Encoded_Once()
    {
        var html = await new ScribanTestRenderer().RenderAsync(
            "{{ title = \"Tips & <tricks>\" }}{{ include \"partials/title\" }}",
            HeadOnlyLayout,
            new Dictionary<string, string> { ["partials/title"] = "<b>{{ title }}</b>" });

        html.ShouldEndWith("<b>Tips &amp; &lt;tricks&gt;</b>");
    }

    [Fact]
    public async Task A_Variable_The_Page_Assigns_Should_Be_Visible_In_The_Partial()
    {
        var html = await new ScribanTestRenderer().RenderAsync(
            "{{ greeting = \"hello\" }}{{ include \"partials/greet\" }}",
            HeadOnlyLayout,
            new Dictionary<string, string> { ["partials/greet"] = "[{{ greeting }}, {{ page.name }}]" });

        html.ShouldEndWith("[hello, about]");
    }

    [Fact]
    public async Task Positional_Arguments_Should_Reach_The_Partial()
    {
        var html = await new ScribanTestRenderer().RenderAsync(
            "{{ include \"partials/args\" \"first\" 2 }}",
            HeadOnlyLayout,
            new Dictionary<string, string> { ["partials/args"] = "[{{ $1 }}|{{ $2 }}]" });

        html.ShouldEndWith("[first|2]");
    }

    [Fact]
    public async Task A_Named_Argument_Should_Reach_The_Partial_As_A_Local_Variable()
    {
        var html = await new ScribanTestRenderer().RenderAsync(
            "{{ include \"partials/named\" title: \"Hi\" }}",
            HeadOnlyLayout,
            new Dictionary<string, string> { ["partials/named"] = "[{{ $title }}]" });

        html.ShouldEndWith("[Hi]");
    }

    [Fact]
    public async Task The_Layout_Should_Include_A_Partial_Too()
    {
        var html = await new ScribanTestRenderer().RenderAsync(
            "<p>page</p>",
            "{{ site_head }}{{ body }}{{ include \"partials/footer\" }}",
            new Dictionary<string, string> { ["partials/footer"] = "<footer>{{ page.display_name }}</footer>" });

        html.ShouldEndWith("<p>page</p><footer>About us</footer>");
    }

    [Fact]
    public async Task A_Partial_Should_Include_Another_Within_The_Limit()
    {
        var renderer = new ScribanTestRenderer(options: new SiteScribanOptions { MaxIncludeDepth = 2 });

        var html = await renderer.RenderAsync(
            "{{ include \"partials/outer\" }}",
            HeadOnlyLayout,
            new Dictionary<string, string>
            {
                ["partials/outer"] = "<ul>{{ include \"/partials/inner\" }}</ul>",
                ["partials/inner"] = "<li>inner</li>"
            });

        html.ShouldEndWith("<ul><li>inner</li></ul>");
    }

    [Theory]
    [InlineData("layout")]
    [InlineData("Default")]
    [InlineData("assets/site.css")]
    public async Task A_Template_That_Is_Not_A_Partial_Should_Not_Be_Includable(string name)
    {
        var ex = await Record.ExceptionAsync(() => new ScribanTestRenderer().RenderAsync(
            $"{{{{ include \"{name}\" }}}}",
            HeadOnlyLayout,
            new Dictionary<string, string>()));

        ex.ShouldBeAssignableTo<ScriptRuntimeException>();
        ex!.Message.ShouldContain("'partials/'");
        ex.Message.ShouldContain($"'{name}'");
    }

    [Fact]
    public async Task An_Unknown_Partial_Should_Fail_Naming_It()
    {
        var ex = await Record.ExceptionAsync(() => new ScribanTestRenderer().RenderAsync(
            "{{ include \"partials/missing\" }}",
            HeadOnlyLayout,
            new Dictionary<string, string>()));

        ex.ShouldBeAssignableTo<ScriptRuntimeException>();
        ex!.Message.ShouldContain("partials/missing");
    }

    /// <summary>
    /// The render set holds the tenant's published partials only; a draft, or another tenant's partial, is not
    /// in it, and so cannot be included, whatever its name.
    /// </summary>
    [Fact]
    public async Task A_Partial_Outside_The_Render_Set_Should_Not_Be_Includable()
    {
        var ex = await Record.ExceptionAsync(() => new ScribanTestRenderer().RenderAsync(
            "{{ include \"partials/draft\" }}",
            HeadOnlyLayout,
            new Dictionary<string, string> { ["partials/published"] = "published" }));

        ex.ShouldBeAssignableTo<ScriptRuntimeException>();
        ex!.Message.ShouldContain("No published template named 'partials/draft'");
    }

    [Fact]
    public async Task A_Cycle_Should_Fail_Naming_The_Chain()
    {
        var ex = await Record.ExceptionAsync(() => new ScribanTestRenderer().RenderAsync(
            "{{ include \"partials/a\" }}",
            HeadOnlyLayout,
            new Dictionary<string, string>
            {
                ["partials/a"] = "a{{ include \"partials/b\" }}",
                ["partials/b"] = "b{{ include \"partials/a\" }}"
            }));

        ex.ShouldBeAssignableTo<ScriptRuntimeException>();
        ex!.Message.ShouldContain("Cyclic");
        ex.Message.ShouldContain("partials/a -> partials/b -> partials/a");
    }

    [Fact]
    public async Task Nesting_Deeper_Than_The_Limit_Should_Fail_Naming_The_Option()
    {
        var renderer = new ScribanTestRenderer(options: new SiteScribanOptions { MaxIncludeDepth = 2 });

        var ex = await Record.ExceptionAsync(() => renderer.RenderAsync(
            "{{ include \"partials/one\" }}",
            HeadOnlyLayout,
            new Dictionary<string, string>
            {
                ["partials/one"] = "{{ include \"partials/two\" }}",
                ["partials/two"] = "{{ include \"partials/three\" }}",
                ["partials/three"] = "three"
            }));

        ex.ShouldBeAssignableTo<ScriptRuntimeException>();
        ex!.Message.ShouldContain(nameof(SiteScribanOptions.MaxIncludeDepth));
        ex.Message.ShouldContain("partials/three");
    }

    /// <summary>It would hand back its partials' output as one plain string, encoded a second time; not offered.</summary>
    [Fact]
    public async Task Include_Join_Should_Not_Exist()
    {
        var ex = await Record.ExceptionAsync(() => new ScribanTestRenderer().RenderAsync(
            "{{ include_join \", \" [\"partials/a\"] }}",
            HeadOnlyLayout,
            new Dictionary<string, string> { ["partials/a"] = "a" }));

        ex.ShouldBeAssignableTo<ScriptRuntimeException>();
        ex!.Message.ShouldContain("include_join");
    }

    /// <summary>A context created without a loader - the sandbox on its own - includes nothing.</summary>
    [Fact]
    public async Task Without_A_Loader_Include_Should_Throw()
    {
        var options = new SiteScribanOptions();
        var sandbox = new ScribanSandbox(Microsoft.Extensions.Options.Options.Create(options), ScribanTestRenderer.Encoder);
        var context = sandbox.CreateContext(CancellationToken.None, new BoundedScriptOutput("test", options.MaxOutputLength));

        var ex = await Record.ExceptionAsync(async () =>
            await ScribanTemplateSyntax.Parse("{{ include \"partials/a\" }}", "test").RenderAsync(context));

        ex.ShouldBeAssignableTo<ScriptRuntimeException>();
    }

    /// <summary>
    /// The engine counts a partial's characters against the output limit when the partial writes them, and the
    /// page writes the same characters again; counted twice - once more per level of nesting - a page made of
    /// partials would be cut off silently long before the limit.
    /// </summary>
    [Fact]
    public async Task A_Partials_Output_Should_Count_Once_Towards_The_Output_Limit()
    {
        var options = new SiteScribanOptions { MaxOutputLength = 100 };
        var x30 = new string('x', 30);

        var html = await RenderInSandboxAsync(
            "{{ include \"partials/a\" }}",
            new Dictionary<string, string>
            {
                ["partials/a"] = "{{ include \"partials/b\" }}",
                ["partials/b"] = "{{ include \"partials/c\" }}",
                ["partials/c"] = x30
            },
            options);

        html.ShouldBe(x30);
        (await RenderInSandboxAsync(
                "{{ include \"partials/big\" }}",
                new Dictionary<string, string> { ["partials/big"] = new string('y', 60) },
                options))
            .ShouldBe(new string('y', 60));
    }

    [Fact]
    public async Task Included_Output_Beyond_The_Limit_Should_Throw()
    {
        var options = new SiteScribanOptions { MaxOutputLength = 100 };

        var ex = await Record.ExceptionAsync(() => RenderInSandboxAsync(
            "{{ include \"partials/big\" }}{{ include \"partials/big\" }}",
            new Dictionary<string, string> { ["partials/big"] = new string('y', 60) },
            options));

        ex.ShouldNotBeNull();
        ex.Message.ShouldContain("100");
    }

    /// <summary>Written a second time, an included value adds characters that were never counted, and is counted.</summary>
    [Fact]
    public async Task An_Included_Value_Written_Twice_Should_Count_The_Second_Time()
    {
        var options = new SiteScribanOptions { MaxOutputLength = 100 };

        var html = await RenderInSandboxAsync(
            "{{ x = include \"partials/a\" }}{{ x }}{{ x }}",
            new Dictionary<string, string> { ["partials/a"] = "<i>a</i>" },
            options);

        html.ShouldBe("<i>a</i><i>a</i>");
    }

    private static async Task<string> RenderInSandboxAsync(
        string body,
        IReadOnlyDictionary<string, string> partials,
        SiteScribanOptions options)
    {
        var wrapped = Microsoft.Extensions.Options.Options.Create(options);
        var sandbox = new ScribanSandbox(wrapped, ScribanTestRenderer.Encoder);
        using var cache = new ScribanTemplateCache(wrapped);
        var loader = new SiteTemplateLoader(
            partials.ToDictionary(p => p.Key, p => ScribanTestRenderer.Version(p.Value)),
            cache);

        using var timeout = new CancellationTokenSource(options.RenderTimeout);
        var context = sandbox.CreateContext(timeout.Token, new BoundedScriptOutput("test", options.MaxOutputLength), loader);

        var template = ScribanTemplateSyntax.Parse(body, "test");
        template.HasErrors.ShouldBeFalse(string.Join("; ", template.Messages));

        return await template.RenderAsync(context);
    }
}
