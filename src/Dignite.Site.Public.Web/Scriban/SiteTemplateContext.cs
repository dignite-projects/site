using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using global::Scriban;
using global::Scriban.Parsing;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// A <see cref="TemplateContext"/> that HTML-encodes every value a template writes, the way Razor does - see
/// <see cref="ScribanSandbox"/>. The template's own literal text is written as it is; a
/// <see cref="ScribanRawHtml"/> value is too. Created by <see cref="ScribanSandbox"/> only.
/// </summary>
public class SiteTemplateContext : TemplateContext
{
    /// <summary>Included partials' outputs not yet written - see <see cref="MarkAsCounted"/>.</summary>
    private readonly HashSet<ScribanRawHtml> _countedOutputs = new();

    public SiteTemplateContext(HtmlEncoder htmlEncoder)
    {
        HtmlEncoder = htmlEncoder;
    }

    /// <summary>The host's encoder, so a host that leaves e.g. CJK text unencoded in Razor gets the same here.</summary>
    public HtmlEncoder HtmlEncoder { get; }

    /// <summary>
    /// The partials being rendered, innermost on top: what <see cref="SiteIncludeFunction"/> checks the next
    /// <c>include</c> against, for a cycle and for <see cref="SiteScribanOptions.MaxIncludeDepth"/>.
    /// </summary>
    public Stack<string> IncludeStack { get; } = new();

    /// <summary>
    /// Marks <paramref name="html"/>, a partial's output (<see cref="SiteIncludeFunction"/>), as already counted
    /// against <see cref="TemplateContext.LimitToString"/>: the engine counted each character when the partial
    /// wrote it into its own buffer, and would count it again when the caller writes the buffer's text - once more
    /// for each level of nesting. A page made mostly of partials would then reach the limit at a fraction of
    /// <see cref="SiteScribanOptions.MaxOutputLength"/>, and the engine truncates silently there rather than
    /// letting <see cref="BoundedScriptOutput"/> fail. So the first write of a marked value goes straight to the
    /// output, uncounted; the output's own bound still applies to it. Only the first: a value written again
    /// (<c>{{ x = include "partials/a" }}{{ x }}{{ x }}</c>) adds characters that were never counted, and is
    /// counted like any other.
    /// </summary>
    public virtual void MarkAsCounted(ScribanRawHtml html)
    {
        _countedOutputs.Add(html);
    }

    public override TemplateContext Write(SourceSpan span, object? textAsObject)
    {
        if (textAsObject is ScribanRawHtml raw && _countedOutputs.Remove(raw))
        {
            Output.Write(raw.Value, 0, raw.Value.Length);
            return this;
        }

        var text = ToOutput(textAsObject);
        if (text != null)
        {
            Write(text);
        }

        return this;
    }

    public override async ValueTask<TemplateContext> WriteAsync(SourceSpan span, object? textAsObject)
    {
        if (textAsObject is ScribanRawHtml raw && _countedOutputs.Remove(raw))
        {
            await Output.WriteAsync(raw.Value, 0, raw.Value.Length, CancellationToken);
            return this;
        }

        var text = ToOutput(textAsObject);
        if (text != null)
        {
            await WriteAsync(text);
        }

        return this;
    }

    /// <summary>The text to write for a value: as is when it is marked raw, HTML-encoded otherwise.</summary>
    protected virtual string? ToOutput(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case ScribanRawHtml raw:
                return raw.Value;
            default:
                var text = ObjectToString(value);
                return string.IsNullOrEmpty(text) ? text : HtmlEncoder.Encode(text);
        }
    }
}
