using System;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Web;
using Dignite.Site.Public.Fields;
using Dignite.Site.Public.Localization;
using Dignite.Site.Public.Routing;
using Dignite.Site.Public.Scriban.Functions;
using Dignite.Site.Public.TagHelpers;
using Dignite.Site.Public.Themes;
using Dignite.Site.Themes;
using global::Scriban.Runtime;
using global::Scriban.Syntax;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace Dignite.Site.Public.Scriban;

/// <summary>
/// Renders one page with a theme's Scriban templates (Tier 0): the page template, then the layout around it, which
/// reads the page template's output as <c>body</c>. The layout is the one the page template's front matter names
/// (<c>layout = "layouts/landing"</c>), else the theme's default layout; <c>layout = "none"</c>, or no default layout,
/// renders the page template alone.
/// <para>
/// Both templates see the same globals (<see cref="ScribanRenderModelBuilder"/>) and functions
/// (<c>site_head</c>, <c>t</c>, <c>url</c>, <c>file_url</c>, <c>asset_url</c>, <c>contents</c>, <c>render_field</c>,
/// <c>find_page</c>, <c>field_definitions</c>, <c>html_raw</c>); a variable the page template assigns (<c>{{ title = "..." }}</c>) is visible to the
/// layout, which is how a page hands its layout a title. Each template renders in its own sandboxed context
/// (<see cref="ScribanSandbox"/>) with its own output bound; the time budget covers both.
/// </para>
/// <para>
/// Both can <c>include</c> the theme's other templates - those that came with the render set, through one
/// <see cref="SiteTemplateLoader"/> per render (<see cref="CreateTemplateLoader"/>). An included template renders inside
/// the context of the template that includes it, so it shares that template's variables, output bound and time
/// budget.
/// </para>
/// <para>
/// A rendered page must contain the platform's SEO head tags, so a render that never called
/// <c>site_head</c> fails with <see cref="SiteErrorCodes.SiteTemplateDidNotRenderSiteHead"/>. The check before a
/// publish already looks for <c>site_head</c>; this catches a call that is there but never reached
/// (<c>{{ if false }}{{ site_head }}{{ end }}</c>).
/// </para>
/// </summary>
public class ScribanPageRenderer : ITransientDependency
{
    protected IOptions<SiteScribanOptions> Options { get; }
    protected ScribanSandbox Sandbox { get; }
    protected ScribanTemplateCache TemplateCache { get; }
    protected ScribanRenderModelBuilder ModelBuilder { get; }
    protected IStringLocalizer<SiteTemplateResource> TemplateLocalizer { get; }
    protected SiteLanguageContext LanguageContext { get; }
    protected IOptions<SiteLanguageOptions> LanguageOptions { get; }
    protected IUrlHelperFactory UrlHelperFactory { get; }
    protected ContentListQuery ContentListQuery { get; }
    protected IRazorPartialRenderer PartialRenderer { get; }
    protected IFieldPublicAppService FieldAppService { get; }
    protected HtmlEncoder HtmlEncoder { get; }

    public ScribanPageRenderer(
        IOptions<SiteScribanOptions> options,
        ScribanSandbox sandbox,
        ScribanTemplateCache templateCache,
        ScribanRenderModelBuilder modelBuilder,
        IStringLocalizer<SiteTemplateResource> templateLocalizer,
        SiteLanguageContext languageContext,
        IOptions<SiteLanguageOptions> languageOptions,
        IUrlHelperFactory urlHelperFactory,
        ContentListQuery contentListQuery,
        IRazorPartialRenderer partialRenderer,
        IFieldPublicAppService fieldAppService,
        HtmlEncoder htmlEncoder)
    {
        Options = options;
        Sandbox = sandbox;
        TemplateCache = templateCache;
        ModelBuilder = modelBuilder;
        TemplateLocalizer = templateLocalizer;
        LanguageContext = languageContext;
        LanguageOptions = languageOptions;
        UrlHelperFactory = urlHelperFactory;
        ContentListQuery = contentListQuery;
        PartialRenderer = partialRenderer;
        FieldAppService = fieldAppService;
        HtmlEncoder = htmlEncoder;
    }

    /// <summary>The page's HTML, and the layout it was rendered in.</summary>
    /// <param name="templatePath">The page's template (<c>Page.Template</c>, normalized), which must be a template of <paramref name="renderSet"/>.</param>
    /// <param name="renderSet">The theme to render with: the live release, or what a preview renders.</param>
    /// <param name="viewModel">The resolved page.</param>
    /// <param name="actionContext">The request being answered.</param>
    /// <exception cref="TimeoutException">The render took longer than <see cref="SiteScribanOptions.RenderTimeout"/>.</exception>
    /// <exception cref="BusinessException"><see cref="SiteErrorCodes.SiteTemplateDidNotRenderSiteHead"/>.</exception>
    /// <exception cref="ScriptRuntimeException">A template failed - its message carries the template path, line and column.</exception>
    /// <exception cref="InvalidOperationException">A template does not parse, or the layout is not a template of the theme.</exception>
    public virtual async Task<ScribanPageRender> RenderAsync(
        string templatePath,
        SiteThemeRenderSet renderSet,
        SiteRenderViewModel viewModel,
        ActionContext actionContext)
    {
        var templateFile = renderSet.FindTemplate(templatePath)
                           ?? throw new ArgumentException($"The theme has no template '{templatePath}' to render.", nameof(templatePath));

        var template = TemplateCache.GetOrAdd(templateFile);
        var layoutPath = GetLayoutPath(template, renderSet);
        var layoutFile = layoutPath == null
            ? null
            : renderSet.FindTemplate(layoutPath)
              ?? throw new InvalidOperationException(
                  $"The layout '{layoutPath}' of the Scriban template '{templatePath}' is not a template of the theme.");

        var options = Options.Value;
        var requestAborted = actionContext.HttpContext.RequestAborted;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        timeout.CancelAfter(options.RenderTimeout);

        var scope = new ScribanRenderScope(viewModel, actionContext, renderSet);
        var globals = await ModelBuilder.BuildGlobalsAsync(viewModel);
        RegisterFunctions(globals, scope);
        var templateLoader = CreateTemplateLoader(renderSet);

        var html = await RenderTemplateAsync(templateFile, globals, templateLoader, timeout.Token, requestAborted);
        var documentName = templatePath;

        if (layoutFile != null)
        {
            // The page template's output is already HTML - encoded where it had to be - so the layout writes it
            // as is.
            globals.SetValue(SiteThemeConsts.BodyVariableName, new ScribanRawHtml(html), true);
            html = await RenderTemplateAsync(layoutFile, globals, templateLoader, timeout.Token, requestAborted);
            documentName = layoutFile.Path;
        }

        if (!scope.SiteHeadCalled)
        {
            throw new BusinessException(SiteErrorCodes.SiteTemplateDidNotRenderSiteHead)
                .WithData("Name", documentName);
        }

        return new ScribanPageRender(html, layoutFile?.Path);
    }

    /// <summary>
    /// The layout a page template renders in: what its front matter's <c>layout</c> names, else the theme's default
    /// layout; null for <c>layout = "none"</c>, or when there is no default.
    /// </summary>
    protected virtual string? GetLayoutPath(global::Scriban.Template pageTemplate, SiteThemeRenderSet renderSet)
    {
        var layout = ScribanTemplateSyntax.ReadFrontMatter(pageTemplate).Layout?.Path ?? renderSet.DefaultLayout;
        return layout == SiteThemeConsts.NoLayout ? null : layout;
    }

    /// <summary>
    /// Adds the functions to <paramref name="globals"/>, read-only, so a template cannot assign over one. A
    /// derived renderer adds its own here.
    /// </summary>
    protected virtual void RegisterFunctions(ScriptObject globals, ScribanRenderScope scope)
    {
        globals.SetValue(SiteHeadFunction.Name, new SiteHeadFunction(scope, HtmlEncoder), true);
        globals.SetValue(TextFunction.Name, new TextFunction(TemplateLocalizer), true);
        globals.SetValue(UrlFunction.Name, new UrlFunction(scope, LanguageContext, LanguageOptions, UrlHelperFactory), true);
        globals.SetValue(FileUrlFunction.Name, new FileUrlFunction(), true);
        globals.SetValue(AssetUrlFunction.Name, new AssetUrlFunction(scope, UrlHelperFactory), true);
        globals.SetValue(ContentsFunction.Name, new ContentsFunction(scope, ContentListQuery, ModelBuilder), true);
        globals.SetValue(ContentCountsFunction.Name, new ContentCountsFunction(scope, ContentListQuery), true);
        globals.SetValue(RenderFieldFunction.Name, new RenderFieldFunction(scope, PartialRenderer), true);
        globals.SetValue(FindPageFunction.Name, new FindPageFunction(scope, ContentListQuery, ModelBuilder), true);
        globals.SetValue(FieldDefinitionsFunction.Name, new FieldDefinitionsFunction(scope, FieldAppService, ModelBuilder), true);
        globals.SetValue(HtmlRawFunction.Name, new HtmlRawFunction(), true);
    }

    /// <summary>
    /// What <c>include</c> may pull in during this render: the theme's templates, which came with the render set -
    /// an include costs no query.
    /// </summary>
    protected virtual ITemplateLoader CreateTemplateLoader(SiteThemeRenderSet renderSet)
    {
        return new SiteTemplateLoader(renderSet, TemplateCache);
    }

    protected virtual async Task<string> RenderTemplateAsync(
        SiteThemeSnapshotFileDto file,
        ScriptObject globals,
        ITemplateLoader templateLoader,
        CancellationToken cancellationToken,
        CancellationToken requestAborted)
    {
        var name = file.Path;
        var template = TemplateCache.GetOrAdd(file);
        var output = new BoundedScriptOutput(name, Options.Value.MaxOutputLength);
        var context = Sandbox.CreateContext(cancellationToken, output, templateLoader);
        context.PushGlobal(globals);

        try
        {
            return await template.RenderAsync(context);
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested
                                   && !requestAborted.IsCancellationRequested
                                   && IsCancellation(ex))
        {
            // The budget ran out, not the visitor's patience - say which template and which limit. A request the
            // client aborted propagates as it is.
            throw new TimeoutException(
                $"Rendering the Scriban template '{name}' took longer than " +
                $"{Options.Value.RenderTimeout.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)} ms, the limit set by " +
                $"{nameof(SiteScribanOptions)}.{nameof(SiteScribanOptions.RenderTimeout)}.",
                ex);
        }
    }

    /// <summary>
    /// Whether <paramref name="exception"/> is, or wraps, a cancellation: the engine's own
    /// <see cref="ScriptAbortException"/> between statements, or an <see cref="OperationCanceledException"/>
    /// from something it awaited, which Scriban may wrap in a <see cref="ScriptRuntimeException"/>.
    /// </summary>
    protected static bool IsCancellation(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is ScriptAbortException or OperationCanceledException)
            {
                return true;
            }
        }

        return false;
    }
}
