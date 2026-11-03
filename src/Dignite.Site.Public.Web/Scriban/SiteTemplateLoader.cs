using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Site.Public.Themes;
using Dignite.Site.Themes;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;
using global::Scriban.Syntax;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// What <c>include</c> may pull in during one page render: the templates of the theme the page renders with, and
/// nothing else - no file of the server, no static file, no other tenant's, no other release's. Created by
/// <see cref="ScribanPageRenderer"/> for each render from its <see cref="SiteThemeRenderSet"/>.
/// <para>
/// Any template of the theme can be included - the program does not tell a partial from a page template; a leading
/// <c>_</c> is only a naming habit. The name is the template's full path from the theme's root.
/// </para>
/// <para>
/// The engine resolves an include in two steps: <see cref="GetPath"/> turns the name into a path, then it looks the
/// path up in <see cref="TemplateContext.CachedTemplates"/> and only on a miss asks <see cref="Load"/>/<see cref="LoadAsync"/>
/// for source text, which it would then parse itself. This loader does all of its work in the first step: it refuses a
/// name the theme has no template for, and otherwise puts the template's parsed tree - from
/// <see cref="ScribanTemplateCache"/>, shared across requests - into the context's cache under its path. So an include
/// costs no query (the theme came with the page) and no parse after the first request, and is parsed with the same
/// syntax rules as the page (<see cref="ScribanTemplateSyntax"/>) rather than the engine's include options.
/// <see cref="Load"/> is therefore never reached; it refuses, as the guard should that ever change.
/// </para>
/// </summary>
public class SiteTemplateLoader : ITemplateLoader
{
    /// <param name="renderSet">The theme being rendered.</param>
    /// <param name="templateCache">Parsed templates, shared across requests.</param>
    /// <param name="cultureChain">
    /// The page's language and its parents, most specific first, in which an included template is looked up for a
    /// version of its own (<c>_card.ja</c> for <c>_card</c>); null or empty for none.
    /// </param>
    public SiteTemplateLoader(SiteThemeRenderSet renderSet, ScribanTemplateCache templateCache, IReadOnlyList<string>? cultureChain = null)
    {
        RenderSet = renderSet;
        TemplateCache = templateCache;
        CultureChain = cultureChain;
    }

    protected SiteThemeRenderSet RenderSet { get; }

    protected ScribanTemplateCache TemplateCache { get; }

    protected IReadOnlyList<string>? CultureChain { get; }

    /// <summary>
    /// The path of the template that is included - the version for the page's language when the named template has one,
    /// else the named template - once its parsed tree is in <see cref="TemplateContext.CachedTemplates"/>.
    /// </summary>
    /// <exception cref="ScriptRuntimeException">No file at that path, a static file, or a template that does not parse.</exception>
    public virtual string GetPath(TemplateContext context, SourceSpan callerSpan, string templateName)
    {
        var named = SiteThemePaths.Normalize(templateName);
        if (RenderSet.FindFile(named) == null)
        {
            throw new ScriptRuntimeException(callerSpan, $"No template '{named}' in the theme to include.");
        }

        if (SiteThemePaths.IsAsset(named))
        {
            throw new ScriptRuntimeException(
                callerSpan,
                $"'{named}' is a static file; only a template - a name without an extension - can be included.");
        }

        // Cached under the path of the file actually included, which is what the engine looks up next.
        var file = RenderSet.FindTemplate(named, CultureChain)!;
        var path = file.Path;

        if (!context.CachedTemplates.ContainsKey(path))
        {
            Template template;
            try
            {
                template = TemplateCache.GetOrAdd(file);
            }
            catch (ScribanRenderException ex)
            {
                throw new ScriptRuntimeException(callerSpan, ex.Message, ex);
            }

            context.CachedTemplates[path] = template;
        }

        return path;
    }

    /// <summary>Never reached - see the class remarks.</summary>
    public virtual string? Load(TemplateContext context, SourceSpan callerSpan, string templatePath)
    {
        throw new ScriptRuntimeException(callerSpan, $"No template '{templatePath}' in the theme to include.");
    }

    /// <summary>Never reached - see the class remarks.</summary>
    public virtual ValueTask<string?> LoadAsync(TemplateContext context, SourceSpan callerSpan, string templatePath)
    {
        throw new ScriptRuntimeException(callerSpan, $"No template '{templatePath}' in the theme to include.");
    }
}
