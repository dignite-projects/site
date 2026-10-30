using System;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Threading;
using global::Scriban;
using global::Scriban.Runtime;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// The only place a <see cref="TemplateContext"/> is created: a template is written by a tenant, so every
/// render runs in a context that treats it as untrusted code. Each knob and the attack it closes:
/// <list type="bullet">
/// <item><see cref="TemplateContext.MemberFilter"/> - reflection on CLR objects. The model handed to a
/// template is pure <see cref="ScriptObject"/>/<see cref="ScriptArray"/>/primitive data
/// (<see cref="ScribanRenderModelBuilder"/>), so nothing reaches a CLR object by design; the filter is the
/// second line, should one slip in. Without it a property returning <see cref="Type"/> is traversable
/// (<c>x.get_type.assembly</c>, ...). Allowed: the instance properties of <see cref="DateTime"/> and
/// <see cref="TimeSpan"/> only (<c>content.publish_time.year</c>), plain values with no way out.</item>
/// <item><see cref="TemplateContext.TemplateLoader"/> - what <c>include</c> can reach: files, other tenants'
/// templates, drafts. The renderer passes a <see cref="SiteTemplateLoader"/>, which offers the tenant's published
/// partials (<c>partials/...</c>) and nothing else; without one (null) <c>include</c> throws. The builtin
/// <c>include</c> itself is replaced by <see cref="SiteIncludeFunction"/> - cycles, nesting depth, and the
/// partial's output written once encoded, not twice - and <c>include_join</c> is removed: it would return the
/// partials' output as one plain string, encoded a second time when written, and is not offered.</item>
/// <item><see cref="TemplateContext.StrictVariables"/> - an unknown variable or function is an error, not
/// silently empty output: a typo fails the render instead of shipping a broken page.</item>
/// <item><see cref="TemplateContext.LoopLimit"/> - <c>for</c>/<c>while</c> iterations and range sizes
/// (<c>1..100000000</c>): CPU and memory exhaustion. Scriban counts the iterations of nested loops together,
/// until the outermost one ends: a loop over 100 items with a loop over 10 fields inside takes 1,100 steps of
/// the budget, not 100 and 10.</item>
/// <item><see cref="TemplateContext.RecursiveLimit"/> - a recursive function: stack overflow, which would take
/// the whole process down rather than fail one request.</item>
/// <item><see cref="TemplateContext.ObjectRecursionLimit"/> - turning a deeply nested or cyclic object into
/// text.</item>
/// <item><see cref="TemplateContext.RegexTimeOut"/> - catastrophic backtracking in <c>regex.*</c>
/// (ReDoS).</item>
/// <item><see cref="TemplateContext.LimitToString"/> plus <see cref="BoundedScriptOutput"/> - output and string
/// size (<c>"x" * 100000000</c>, a loop appending to a string): memory exhaustion. Scriban's own limit
/// truncates silently; the bounded output turns the overflow into an error that names the template.</item>
/// <item><see cref="TemplateContext.CancellationToken"/> - wall-clock time (<see cref="SiteScribanOptions.RenderTimeout"/>,
/// linked to the request's abort token by the caller): nested loops that each stay under
/// <see cref="TemplateContext.LoopLimit"/>, or loops calling functions that each take a while.</item>
/// <item><see cref="SiteTemplateContext"/> - HTML injection. Every value a template writes is HTML-encoded
/// with the host's <see cref="HtmlEncoder"/>, as Razor does: data comes from content editors and, through route
/// placeholders (<c>filter_values</c>, the filter suffix of <c>head.meta_title</c>), straight from the URL, so a
/// template that forgot to escape would be a reflected XSS. Written as is: the template's own literal text,
/// and <see cref="ScribanRawHtml"/> - what <c>site_head</c> and <c>render_field</c> return, the layout's
/// <c>body</c>, and whatever a template passes through <c>html_raw</c>. The mark survives being assigned and
/// written (<c>{{ x = render_field "body" }}{{ x }}</c>); a string function or concatenation turns a marked
/// value into plain text, which is then encoded - the safe direction. So does <c>capture</c>: what it collects is
/// already encoded output, held as a plain string, so <c>{{ capture x }}...{{ end }}{{ x }}</c> encodes it a second
/// time; <c>{{ x | html_raw }}</c> writes it as captured, which is safe because its values were encoded on the way
/// in.</item>
/// <item><see cref="TemplateContext.AutoIndent"/> = false - Scriban would otherwise re-indent every line of a
/// multi-line value written after leading whitespace, which changes the content of a <c>&lt;pre&gt;</c> or
/// <c>&lt;textarea&gt;</c> inside rendered HTML. Output is exactly what the template and its values say.</item>
/// </list>
/// </summary>
public class ScribanSandbox : ITransientDependency
{
    /// <summary>The builtin that includes several templates at once - not offered, see the class remarks.</summary>
    public const string IncludeJoinFunctionName = "include_join";

    protected IOptions<SiteScribanOptions> Options { get; }
    protected HtmlEncoder HtmlEncoder { get; }

    public ScribanSandbox(IOptions<SiteScribanOptions> options, HtmlEncoder htmlEncoder)
    {
        Options = options;
        HtmlEncoder = htmlEncoder;
    }

    /// <summary>
    /// A fresh, locked-down context writing to <paramref name="output"/>. One per template render: a context
    /// is not thread-safe, and its output-length bookkeeping is per render.
    /// </summary>
    /// <param name="cancellationToken">Stops the render: the time budget, linked to the request's abort token.</param>
    /// <param name="output">Where the template writes.</param>
    /// <param name="templateLoader">What <c>include</c> may pull in; null, and <c>include</c> throws.</param>
    public virtual TemplateContext CreateContext(
        CancellationToken cancellationToken,
        IScriptOutput output,
        ITemplateLoader? templateLoader = null)
    {
        var options = Options.Value;

        var context = new SiteTemplateContext(HtmlEncoder)
        {
            AutoIndent = false,
            MemberFilter = IsAllowedMember,
            TemplateLoader = templateLoader,
            StrictVariables = true,
            LoopLimit = options.LoopLimit,
            RecursiveLimit = options.RecursiveLimit,
            ObjectRecursionLimit = options.ObjectRecursionLimit,
            // One more than the output may hold, so an overflow reaches BoundedScriptOutput - which throws
            // with a message naming the template - rather than Scriban's silent "..." truncation. The same
            // bound applies to every string a template builds (multiplication, join, pad).
            LimitToString = options.MaxOutputLength + 1,
            RegexTimeOut = options.RegexTimeout,
            CancellationToken = cancellationToken
        };

        // TemplateContext.Output has no setter; a pushed output is what Template.RenderAsync writes to and
        // returns the text of.
        context.PushOutput(output);

        // The builtins live in an object of this context's own - Scriban copies its defaults into a fresh one per
        // context - so changing them here touches no other render. Replaced in place rather than shadowed by a
        // global pushed on top: nothing then remains anywhere in the context that reaches the engine's own
        // include. SetValue, unlike an assignment from a template, replaces an entry that is read-only. (A template
        // can still shadow the name with a variable of its own, as it can any builtin's; that only breaks its own
        // includes.)
        context.BuiltinObject.SetValue(SiteIncludeFunction.Name, new SiteIncludeFunction(options.MaxIncludeDepth), true);
        context.BuiltinObject.Remove(IncludeJoinFunctionName);

        return context;
    }

    /// <summary>
    /// <see cref="TemplateContext.MemberFilter"/>: nothing of a CLR object, except the plain values of a date or
    /// a duration - see the class remarks.
    /// </summary>
    protected static bool IsAllowedMember(MemberInfo member)
    {
        return member is PropertyInfo property &&
               (property.DeclaringType == typeof(DateTime) || property.DeclaringType == typeof(TimeSpan));
    }
}
