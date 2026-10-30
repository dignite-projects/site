namespace Dignite.Site.Public.Scriban;

/// <summary>
/// HTML a Scriban template writes as it is, unencoded - the one exception to <see cref="SiteTemplateContext"/>
/// encoding everything else. Produced only by the platform: <c>site_head</c>, <c>render_field</c>, the
/// layout's <c>body</c>, and <c>html_raw</c> for a template author who takes responsibility for a value.
/// <para>
/// Opaque to a template - the member filter hides <see cref="Value"/> - and turned into its text by any
/// function that takes a string (<c>body | string.size</c>), which then is ordinary, encoded text again: a
/// marked value loses its mark when it passes through a function, never the other way round.
/// </para>
/// </summary>
public sealed class ScribanRawHtml
{
    public ScribanRawHtml(string value)
    {
        Value = value;
    }

    public string Value { get; }

    /// <summary>The HTML itself - what Scriban uses wherever it turns the value into text.</summary>
    public override string ToString() => Value;
}
