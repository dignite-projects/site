using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Public.Routing;
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

    /// <summary>A string member of an options object, null when missing or empty.</summary>
    protected static string? GetOptionString(TemplateContext context, SourceSpan span, ScriptObject? options, string name)
    {
        if (options == null || !options.TryGetValue(context, span, name, out var value) || value == null)
        {
            return null;
        }

        var text = context.ObjectToString(value);
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>An integer member of an options object, null when missing.</summary>
    protected static int? GetOptionInt(TemplateContext context, SourceSpan span, ScriptObject? options, string name)
    {
        return options != null && options.TryGetValue(context, span, name, out var value) && value != null
            ? context.ToInt(span, value)
            : null;
    }

    /// <summary>A date member of an options object - a date, or a text that reads as one - null when missing.</summary>
    protected static DateTime? GetOptionDate(TemplateContext context, SourceSpan span, ScriptObject? options, string name)
    {
        if (options == null || !options.TryGetValue(context, span, name, out var value) || value == null)
        {
            return null;
        }

        return value switch
        {
            DateTime date => date,
            DateTimeOffset offset => offset.UtcDateTime,
            string text when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) => parsed,
            _ => throw new ScriptRuntimeException(span, $"The option `{name}` takes a date, e.g. date.parse \"2026-01-01\".")
        };
    }

    /// <summary>
    /// An object member of an options object as field name to value, each value as text (<c>true</c> for a ticked box,
    /// <c>design</c> for an option); null when missing. An empty value is left out.
    /// </summary>
    protected static Dictionary<string, string>? GetOptionStringMap(TemplateContext context, SourceSpan span, ScriptObject? options, string name)
    {
        if (options == null || !options.TryGetValue(context, span, name, out var value) || value == null)
        {
            return null;
        }

        if (value is not ScriptObject map)
        {
            throw new ScriptRuntimeException(span, $"The option `{name}` takes an object of field names and values, e.g. {{ category: \"news\" }}.");
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in map.GetMembers())
        {
            map.TryGetValue(context, span, key, out var item);
            var text = item switch
            {
                null => null,
                bool flag => flag ? "true" : "false",
                _ => context.ObjectToString(item)
            };

            if (!string.IsNullOrEmpty(text))
            {
                result[key] = text;
            }
        }

        return result;
    }

    /// <summary>
    /// The filters a list or a count applies: on the current page, the route's (<c>field_filters</c>,
    /// <c>published_after</c>, <c>published_before</c>), with the options' own on top of them - a field the options name
    /// replaces the route's value for it, a bound the options set replaces the route's; on another page, the options'
    /// alone, since that page's route did not answer this request.
    /// </summary>
    protected static (IReadOnlyDictionary<string, string>? FieldFilters, DateTime? PublishedAfter, DateTime? PublishedBefore) GetFilters(
        TemplateContext context, SourceSpan span, ScriptObject? options, SiteRenderViewModel viewModel, bool isCurrentPage)
    {
        var fieldFilters = GetOptionStringMap(context, span, options, "field_filters");
        var publishedAfter = GetOptionDate(context, span, options, "published_after");
        var publishedBefore = GetOptionDate(context, span, options, "published_before");

        if (!isCurrentPage)
        {
            return (fieldFilters, publishedAfter, publishedBefore);
        }

        IReadOnlyDictionary<string, string> merged = viewModel.FieldFilters;
        if (fieldFilters is { Count: > 0 })
        {
            var combined = new Dictionary<string, string>(viewModel.FieldFilters, StringComparer.OrdinalIgnoreCase);
            foreach (var (field, value) in fieldFilters)
            {
                combined[field] = value;
            }

            merged = combined;
        }

        return (merged, publishedAfter ?? viewModel.PublishedAfter, publishedBefore ?? viewModel.PublishedBefore);
    }

    /// <summary>A string argument, null when missing or empty.</summary>
    protected static string? OptionalString(ScriptArray arguments, int index)
    {
        return index < arguments.Count && arguments[index] is string { Length: > 0 } text ? text : null;
    }
}
