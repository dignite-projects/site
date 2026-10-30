using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Site.Templates;
using global::Scriban;
using global::Scriban.Runtime;
using global::Scriban.Syntax;
using Shouldly;
using Xunit;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// A tenant's template is untrusted code: each test is one way out of, or around, the sandbox, and shows it
/// refused - by an error, or by rendering nothing where that is the defence.
/// </summary>
public class ScribanSandbox_Tests
{
    [Fact]
    public async Task A_Method_Of_A_Clr_Object_Should_Not_Be_Callable()
    {
        var ex = await Record.ExceptionAsync(() => RenderAsync("{{ x.secret_method() }}", globals: WithSecret()));

        ex.ShouldNotBeNull();
        ex.Message.ShouldNotContain(Secret.Leaked);
    }

    /// <summary>The member filter: a property of a CLR object is invisible - <c>x.ty</c> would reach <see cref="Type"/>.</summary>
    [Fact]
    public async Task A_Property_Of_A_Clr_Object_Should_Render_Empty()
    {
        (await RenderAsync("[{{ x.ty }}][{{ x.ty?.assembly }}][{{ x.name }}]", globals: WithSecret())).ShouldBe("[][][]");
    }

    /// <summary>The control for the test above: without the filter, the same template does reach the assembly.</summary>
    [Fact]
    public async Task Without_The_Member_Filter_A_Type_Would_Be_Traversable()
    {
        var context = new TemplateContext();
        context.PushGlobal(WithSecret());

        var output = await Template.Parse("{{ x.ty.assembly }}").RenderAsync(context);

        output.ShouldContain(typeof(Secret).Assembly.GetName().Name!);
    }

    [Fact]
    public async Task A_Dates_Own_Members_Should_Stay_Readable()
    {
        var globals = new ScriptObject();
        globals.SetValue("d", new DateTime(2026, 8, 15), false);

        (await RenderAsync("{{ d.year }}-{{ d.month }}-{{ d.day }}", globals: globals)).ShouldBe("2026-8-15");
    }

    [Fact]
    public async Task Include_Should_Throw()
    {
        var ex = await Record.ExceptionAsync(() => RenderAsync("{{ include \"layout\" }}"));

        ex.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_Range_Beyond_The_Loop_Limit_Should_Throw()
    {
        var ex = await Record.ExceptionAsync(() => RenderAsync("{{ for i in 1..50000 }}{{ end }}"));

        ex.ShouldBeAssignableTo<ScriptRuntimeException>();
    }

    [Fact]
    public async Task Unbounded_Recursion_Should_Throw()
    {
        var ex = await Record.ExceptionAsync(() => RenderAsync("{{ func recurse\n ret recurse($0 + 1)\nend\nrecurse 1 }}"));

        ex.ShouldBeAssignableTo<ScriptRuntimeException>();
    }

    /// <summary>Catastrophic backtracking either times out or is optimised away; either way it is over quickly.</summary>
    [Fact]
    public async Task A_Catastrophic_Regex_Should_Not_Run_Away()
    {
        var stopwatch = Stopwatch.StartNew();

        await Record.ExceptionAsync(() => RenderAsync("{{ \"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!\" | regex.match \"^(a+)+$\" }}"));

        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Output_Beyond_The_Limit_Should_Throw_Naming_The_Template()
    {
        var options = new SiteScribanOptions { MaxOutputLength = 100 };

        var ex = await Record.ExceptionAsync(() => RenderAsync("{{ for i in 1..50 }}xxxxxxxxxx{{ end }}", options));

        ex.ShouldNotBeNull();
        ex.Message.ShouldContain("'test'");
        ex.Message.ShouldContain("100");
    }

    [Fact]
    public async Task Output_Within_The_Limit_Should_Render()
    {
        var options = new SiteScribanOptions { MaxOutputLength = 100 };

        (await RenderAsync("{{ for i in 1..10 }}xxxxxxxxxx{{ end }}", options)).Length.ShouldBe(100);
    }

    /// <summary>
    /// Scriban counts nested loops' iterations together until the outermost loop ends, so loops that each stay
    /// under the limit are refused once they multiply past it - long before any clock runs out.
    /// </summary>
    [Fact]
    public async Task Nested_Loops_Should_Share_One_Loop_Limit()
    {
        var ex = await Record.ExceptionAsync(() => RenderAsync(
            "{{ for i in 1..200 }}{{ for j in 1..200 }}{{ end }}{{ end }}"));

        ex.ShouldBeAssignableTo<ScriptRuntimeException>();
        ex!.Message.ShouldContain("iteration limit");
    }

    /// <summary>With the loop limit off, the clock is what stops a runaway template.</summary>
    [Fact]
    public async Task A_Runaway_Template_Should_Be_Stopped_By_The_Clock()
    {
        var options = new SiteScribanOptions { RenderTimeout = TimeSpan.FromMilliseconds(200), LoopLimit = 0 };
        var stopwatch = Stopwatch.StartNew();

        var ex = await Record.ExceptionAsync(() => RenderAsync(
            "{{ for i in 1..1000 }}{{ for j in 1..1000 }}{{ for k in 1..1000 }}{{ end }}{{ end }}{{ end }}", options));

        ex.ShouldNotBeNull();
        IsOrWrapsCancellation(ex).ShouldBeTrue(ex.ToString());
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task A_Written_Value_Should_Be_Html_Encoded_And_Literal_Text_Not()
    {
        var globals = new ScriptObject();
        globals.SetValue("title", "Tips & <script>alert(1)</script>", false);

        (await RenderAsync("<h1>{{ title }}</h1>", globals: globals))
            .ShouldBe("<h1>Tips &amp; &lt;script&gt;alert(1)&lt;/script&gt;</h1>");
    }

    [Fact]
    public async Task A_Raw_Value_Should_Be_Written_As_It_Is_Until_A_String_Function_Touches_It()
    {
        var globals = new ScriptObject();
        globals.SetValue("raw", new ScribanRawHtml("<b>x</b>"), false);

        (await RenderAsync("{{ raw }}|{{ copy = raw }}{{ copy }}|{{ raw | string.upcase }}|{{ raw.value }}", globals: globals))
            .ShouldBe("<b>x</b>|<b>x</b>|&lt;B&gt;X&lt;/B&gt;|");
    }

    [Fact]
    public async Task Multi_Line_Values_Should_Not_Be_Re_Indented()
    {
        var globals = new ScriptObject();
        globals.SetValue("pre", new ScribanRawHtml("<pre>a\nb</pre>"), false);

        (await RenderAsync("    {{ pre }}", globals: globals)).ShouldBe("    <pre>a\nb</pre>");
    }

    [Fact]
    public async Task An_Unknown_Variable_Should_Throw()
    {
        var ex = await Record.ExceptionAsync(() => RenderAsync("{{ typo_var }}"));

        ex.ShouldBeAssignableTo<ScriptRuntimeException>();
    }

    [Fact]
    public async Task A_Missing_Member_Of_Known_Data_Should_Render_Empty()
    {
        var content = new ScriptObject();
        content.SetValue("fields", new ScriptObject(), false);
        var globals = new ScriptObject();
        globals.SetValue("content", content, false);

        (await RenderAsync("[{{ content.fields.missing }}]", globals: globals)).ShouldBe("[]");
    }

    private static async Task<string> RenderAsync(string body, SiteScribanOptions? options = null, ScriptObject? globals = null)
    {
        options ??= new SiteScribanOptions();
        var sandbox = new ScribanSandbox(Microsoft.Extensions.Options.Options.Create(options), ScribanTestRenderer.Encoder);

        using var timeout = new CancellationTokenSource(options.RenderTimeout);
        var context = sandbox.CreateContext(timeout.Token, new BoundedScriptOutput("test", options.MaxOutputLength));
        if (globals != null)
        {
            context.PushGlobal(globals);
        }

        var template = ScribanTemplateSyntax.Parse(body, "test");
        template.HasErrors.ShouldBeFalse(string.Join("; ", template.Messages));

        return await template.RenderAsync(context);
    }

    private static bool IsOrWrapsCancellation(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is ScriptAbortException or OperationCanceledException)
            {
                return true;
            }
        }

        return false;
    }

    private static ScriptObject WithSecret()
    {
        var globals = new ScriptObject();
        globals.SetValue("x", new Secret(), false);
        return globals;
    }

    public class Secret
    {
        public const string Leaked = "leaked";

        public Type Ty => typeof(Secret);

        public string Name => "secret";

        public string SecretMethod() => Leaked;
    }
}
