using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Site.Templates;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;
using global::Scriban.Syntax;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// What <c>include</c> may pull in during one page render: the tenant's published partials, and nothing else -
/// no file, no other kind of template, no draft. Created by <see cref="ScribanPageRenderer"/> for each render
/// from the render set's <see cref="Dignite.Site.Public.Templates.SiteTemplateRenderSetDto.Partials"/>.
/// <para>
/// The engine resolves an include in two steps: <see cref="GetPath"/> turns the name into a path, then it
/// looks the path up in <see cref="TemplateContext.CachedTemplates"/> and only on a miss asks
/// <see cref="Load"/>/<see cref="LoadAsync"/> for source text, which it would then parse itself. This loader does
/// all of its work in the first step: it refuses a name that is not a partial or not published, and otherwise
/// puts the partial's parsed tree - from <see cref="ScribanTemplateCache"/>, keyed by the version's id like the
/// page template's and shared across requests - into the context's cache under its name. So an include costs no
/// query (the partials came with the page's one) and no parse after the first request, and is parsed with the
/// same syntax rules as the page (<see cref="ScribanTemplateSyntax"/>) rather than the engine's include
/// options. <see cref="Load"/> is therefore never reached; it refuses, as the guard should that ever change.
/// </para>
/// </summary>
public class SiteTemplateLoader : ITemplateLoader
{
    public SiteTemplateLoader(IReadOnlyDictionary<string, SiteTemplateVersionDto> partials, ScribanTemplateCache templateCache)
    {
        Partials = partials;
        TemplateCache = templateCache;
    }

    /// <summary>The published version of every partial, keyed by full name (<c>partials/post-card</c>).</summary>
    protected IReadOnlyDictionary<string, SiteTemplateVersionDto> Partials { get; }

    protected ScribanTemplateCache TemplateCache { get; }

    /// <summary>
    /// The normalized name, once the partial's parsed tree is in <see cref="TemplateContext.CachedTemplates"/>.
    /// </summary>
    /// <exception cref="ScriptRuntimeException">
    /// The name is not under <see cref="SiteTemplateConsts.PartialsPrefix"/>, no partial of that name is
    /// published, or its published body does not parse.
    /// </exception>
    public virtual string GetPath(TemplateContext context, SourceSpan callerSpan, string templateName)
    {
        var name = SiteTemplateNames.Normalize(templateName);

        if (!SiteTemplateNames.IsPartial(name))
        {
            throw new ScriptRuntimeException(
                callerSpan,
                $"Only a template under '{SiteTemplateConsts.PartialsPrefix}' can be included; '{name}' is not one.");
        }

        if (!Partials.TryGetValue(name, out var version))
        {
            throw new ScriptRuntimeException(callerSpan, $"No published template named '{name}' to include.");
        }

        if (!context.CachedTemplates.ContainsKey(name))
        {
            Template template;
            try
            {
                template = TemplateCache.GetOrAdd(version, name);
            }
            catch (InvalidOperationException ex)
            {
                throw new ScriptRuntimeException(callerSpan, ex.Message, ex);
            }

            context.CachedTemplates[name] = template;
        }

        return name;
    }

    /// <summary>Never reached - see the class remarks.</summary>
    public virtual string? Load(TemplateContext context, SourceSpan callerSpan, string templatePath)
    {
        throw new ScriptRuntimeException(callerSpan, $"No published template named '{templatePath}' to include.");
    }

    /// <summary>Never reached - see the class remarks.</summary>
    public virtual ValueTask<string?> LoadAsync(TemplateContext context, SourceSpan callerSpan, string templatePath)
    {
        throw new ScriptRuntimeException(callerSpan, $"No published template named '{templatePath}' to include.");
    }
}
