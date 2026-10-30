using System;
using System.Linq;
using System.Threading.Tasks;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;
using global::Scriban.Syntax;

namespace Dignite.Site.Public.Scriban.Functions;

/// <summary>
/// Base of the functions Site adds to a template (<c>site_head</c>, <c>t</c>, <c>url</c>, ...): declared
/// parameters instead of reflection over a method, so what a template may pass is stated in one place, and the
/// engine checks the count and converts each argument to the declared type before <see cref="InvokeAsync(TemplateContext, SourceSpan, ScriptArray)"/>
/// runs. Optional parameters (those with a default) must come after the required ones; a missing one arrives
/// as its default.
/// </summary>
public abstract class SiteScriptFunction : IScriptCustomFunction
{
    private readonly ScriptParameterInfo[] _parameters;

    protected SiteScriptFunction(params ScriptParameterInfo[] parameters)
    {
        _parameters = parameters;
        RequiredParameterCount = parameters.TakeWhile(p => !p.HasDefaultValue).Count();
    }

    public int RequiredParameterCount { get; }

    public int ParameterCount => _parameters.Length;

    public ScriptVarParamKind VarParamKind => ScriptVarParamKind.None;

    public Type ReturnType => typeof(object);

    public ScriptParameterInfo GetParameterInfo(int index)
    {
        // Past the declared ones: an untyped slot, so the engine goes on to report "invalid number of
        // arguments" rather than failing on the lookup itself.
        return index >= 0 && index < _parameters.Length
            ? _parameters[index]
            : new ScriptParameterInfo(typeof(object), "argument" + index);
    }

    public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        return InvokeAsync(context, callerContext?.Span ?? context.CurrentSpan, arguments);
    }

    /// <summary>
    /// The synchronous entry point, for the rare engine path that calls a function synchronously even inside
    /// <c>RenderAsync</c>. A function that completes synchronously costs nothing here; one that awaits a
    /// service blocks this thread until it finishes, which is safe under ASP.NET Core (no synchronization
    /// context) though wasteful - every normal call goes through <see cref="InvokeAsync(TemplateContext, ScriptNode, ScriptArray, ScriptBlockStatement)"/>.
    /// </summary>
    public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        var pending = InvokeAsync(context, callerContext, arguments, blockStatement);
        return pending.IsCompletedSuccessfully ? pending.Result : pending.AsTask().GetAwaiter().GetResult();
    }

    /// <param name="context">The template context.</param>
    /// <param name="span">Where in the template the call is, for errors.</param>
    /// <param name="arguments">Exactly <see cref="ParameterCount"/> values, each converted to its declared type (or its default).</param>
    protected abstract ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments);

    protected static ScriptParameterInfo Required<T>(string name) => new(typeof(T), name);

    protected static ScriptParameterInfo Optional<T>(string name, object? defaultValue = null) => new(typeof(T), name, defaultValue);

    /// <summary>A string argument, null when missing or empty.</summary>
    protected static string? OptionalString(ScriptArray arguments, int index)
    {
        return index < arguments.Count && arguments[index] is string { Length: > 0 } text ? text : null;
    }
}
