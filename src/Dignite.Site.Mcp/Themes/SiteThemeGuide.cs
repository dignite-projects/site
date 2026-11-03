using System;
using System.IO;
using System.Text;

namespace Dignite.Site.Mcp.Themes;

/// <summary>
/// What a model needs to know to write a template: the variables a template sees, the functions it can call, how a
/// layout and a partial are written, what the sandbox refuses. It is the user documentation page
/// <c>docs/en/rendering/scriban-templates.md</c> itself, embedded when this project is built, so the guide a client
/// reads over MCP and the page a person reads cannot drift apart - there is no second text to keep in step.
/// <para>
/// A model that is handed only the tool descriptions writes templates from what it remembers of Liquid or Handlebars,
/// and the sandbox accepts none of it: a template reaches only the variables and functions this page lists.
/// </para>
/// </summary>
public static class SiteThemeGuide
{
    /// <summary>The manifest name of the embedded page, set in the project file.</summary>
    public const string ResourceName = "Dignite.Site.Mcp.Themes.theme-guide.md";

    private static readonly Lazy<string> Loaded = new(Load);

    /// <summary>The guide as Markdown, from its title on.</summary>
    public static string Text => Loaded.Value;

    private static string Load()
    {
        using var stream = typeof(SiteThemeGuide).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException(
                               $"The theme guide is not embedded in {typeof(SiteThemeGuide).Assembly.GetName().Name}: " +
                               $"'{ResourceName}' is missing, so the project file does not embed docs/en/rendering/scriban-templates.md.");
        using var reader = new StreamReader(stream, Encoding.UTF8);

        return SkipFrontMatter(reader.ReadToEnd());
    }

    /// <summary>
    /// The documentation page starts with a block for the documentation site's search engine, which says nothing to a
    /// model: the guide starts at the page's title.
    /// </summary>
    private static string SkipFrontMatter(string page)
    {
        if (page.StartsWith("# ", StringComparison.Ordinal))
        {
            return page;
        }

        var title = page.IndexOf("\n# ", StringComparison.Ordinal);

        return title < 0 ? page : page[(title + 1)..];
    }
}
