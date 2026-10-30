using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Templates;
using global::Scriban;
using global::Scriban.Functions;
using global::Scriban.Runtime;
using global::Scriban.Syntax;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// <c>include "partials/name" [arguments...]</c>: renders a published partial in place - Scriban's own
/// <see cref="IncludeFunction"/>, which <see cref="ScribanSandbox"/> replaces with this in every context, with
/// three things added around it:
/// <list type="bullet">
/// <item>A cycle (<c>partials/a</c> including <c>partials/b</c> including <c>partials/a</c>) fails at once, naming
/// the chain, instead of running into <see cref="TemplateContext.RecursiveLimit"/> with a message about function
/// calls.</item>
/// <item>Nesting deeper than <see cref="SiteScribanOptions.MaxIncludeDepth"/> fails, naming the option.</item>
/// <item>The partial's output is returned as <see cref="ScribanRawHtml"/>. The engine renders a partial in the same
/// context as its caller - same globals, same encoding: every value the partial writes is HTML-encoded as it is
/// written, its literal markup is not - and returns what it wrote as a plain string, which the caller would then
/// write, and so encode, a second time. Marked raw, it is written as the partial produced it, exactly once
/// encoded where it had to be.</item>
/// </list>
/// What the engine already does is unchanged: the partial sees the caller's variables, named arguments become
/// its local variables, positional ones <c>$1</c>, <c>$2</c>... (<c>$0</c> is the name), and which templates
/// can be included at all is <see cref="SiteTemplateLoader"/>'s decision.
/// </summary>
public class SiteIncludeFunction : IScriptCustomFunction
{
    public const string Name = "include";

    public SiteIncludeFunction(int maxIncludeDepth)
    {
        MaxIncludeDepth = maxIncludeDepth;
    }

    /// <summary>The engine's own <c>include</c>, which does the actual work.</summary>
    protected IScriptCustomFunction Inner { get; } = new IncludeFunction();

    /// <summary><see cref="SiteScribanOptions.MaxIncludeDepth"/>.</summary>
    protected int MaxIncludeDepth { get; }

    public int RequiredParameterCount => Inner.RequiredParameterCount;

    public int ParameterCount => Inner.ParameterCount;

    public ScriptVarParamKind VarParamKind => Inner.VarParamKind;

    public Type ReturnType => Inner.ReturnType;

    public ScriptParameterInfo GetParameterInfo(int index)
    {
        return Inner.GetParameterInfo(index);
    }

    public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        var includeStack = Enter(context, callerContext, arguments);
        try
        {
            return ToOutput(context, Inner.Invoke(context, callerContext, arguments, blockStatement));
        }
        finally
        {
            includeStack?.Pop();
        }
    }

    public async ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        var includeStack = Enter(context, callerContext, arguments);
        try
        {
            return ToOutput(context, await Inner.InvokeAsync(context, callerContext, arguments, blockStatement));
        }
        finally
        {
            includeStack?.Pop();
        }
    }

    /// <summary>
    /// Checks the include against the partials already being rendered and pushes its name. Returns the stack
    /// pushed onto, for the caller to pop; null when there is no name to push, which the engine then reports.
    /// </summary>
    /// <exception cref="ScriptRuntimeException">A cycle, or nesting deeper than <see cref="MaxIncludeDepth"/>.</exception>
    protected virtual Stack<string>? Enter(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments)
    {
        var siteContext = context as SiteTemplateContext
                          ?? throw new InvalidOperationException(
                              $"{nameof(SiteIncludeFunction)} runs in a {nameof(SiteTemplateContext)} only, which {nameof(ScribanSandbox)} creates.");

        var templateName = arguments.Count > 0 ? context.ObjectToString(arguments[0]) : null;
        if (string.IsNullOrWhiteSpace(templateName))
        {
            return null;
        }

        var name = SiteTemplateNames.Normalize(templateName);
        var includeStack = siteContext.IncludeStack;
        var span = callerContext?.Span ?? context.CurrentSpan;

        if (includeStack.Contains(name))
        {
            throw new ScriptRuntimeException(span, $"Cyclic include: {FormatChain(includeStack, name)}.");
        }

        if (includeStack.Count >= MaxIncludeDepth)
        {
            throw new ScriptRuntimeException(
                span,
                $"Include depth exceeds {MaxIncludeDepth}, the limit set by " +
                $"{nameof(SiteScribanOptions)}.{nameof(SiteScribanOptions.MaxIncludeDepth)}: {FormatChain(includeStack, name)}.");
        }

        includeStack.Push(name);
        return includeStack;
    }

    /// <summary>
    /// The partial's output, marked raw - see the class remarks - and handed to the context as output already
    /// counted (<see cref="SiteTemplateContext.MarkAsCounted"/>). Anything other than a string passes through.
    /// </summary>
    protected virtual object? ToOutput(TemplateContext context, object? result)
    {
        if (result is not string html)
        {
            return result;
        }

        var raw = new ScribanRawHtml(html);
        (context as SiteTemplateContext)?.MarkAsCounted(raw);
        return raw;
    }

    /// <summary><c>partials/a -&gt; partials/b -&gt; partials/a</c>: outermost first, the refused one last.</summary>
    protected static string FormatChain(Stack<string> includeStack, string name)
    {
        // A stack enumerates from the top, i.e. innermost first.
        return string.Join(" -> ", includeStack.Reverse().Append(name));
    }
}
