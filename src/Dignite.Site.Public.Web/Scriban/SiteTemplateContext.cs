using System;
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
    public SiteTemplateContext(HtmlEncoder htmlEncoder)
    {
        HtmlEncoder = htmlEncoder;
    }

    /// <summary>The host's encoder, so a host that leaves e.g. CJK text unencoded in Razor gets the same here.</summary>
    public HtmlEncoder HtmlEncoder { get; }

    public override TemplateContext Write(SourceSpan span, object? textAsObject)
    {
        var text = ToOutput(textAsObject);
        if (text != null)
        {
            Write(text);
        }

        return this;
    }

    public override async ValueTask<TemplateContext> WriteAsync(SourceSpan span, object? textAsObject)
    {
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
