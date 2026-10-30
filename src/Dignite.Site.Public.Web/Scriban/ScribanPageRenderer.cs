using System;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Web;
using Dignite.Site.Public.Localization;
using Dignite.Site.Public.Routing;
using Dignite.Site.Public.Scriban.Functions;
using Dignite.Site.Public.TagHelpers;
using Dignite.Site.Public.Templates;
using Dignite.Site.Public.Templating;
using Dignite.Site.Templates;
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
/// Renders one page with the tenant's published Scriban templates (Tier 0): the page template, then - when the
/// tenant has published one - the <c>layout</c> around it, which reads the page template's output as
/// <c>body</c>.
/// <para>
/// Both templates see the same globals (<see cref="ScribanRenderModelBuilder"/>) and functions
/// (<c>site_head</c>, <c>t</c>, <c>url</c>, <c>file_url</c>, <c>asset_url</c>, <c>contents</c>, <c>render_field</c>,
/// <c>select_options</c>, <c>html_raw</c>); a variable the page template assigns (<c>{{ title = "..." }}</c>) is visible to the
/// layout, which is how a page hands its layout a title. Each template renders in its own sandboxed context
/// (<see cref="ScribanSandbox"/>) with its own output bound; the time budget covers both.
/// </para>
/// <para>
/// Both can <c>include</c> the tenant's published partials - those that came with the render set, through one
/// <see cref="SiteTemplateLoader"/> per render (<see cref="CreateTemplateLoader"/>). A partial renders inside the
/// context of the template that includes it, so it shares that template's variables, output bound and time
/// budget.
/// </para>
/// <para>
/// A rendered page must contain the platform's SEO head tags, so a render that never called
/// <c>site_head</c> fails with <see cref="SiteErrorCodes.SiteTemplateDidNotRenderSiteHead"/>. Saving a layout
/// already checks it mentions <c>site_head</c>; this catches a page without a layout, and a call that is
/// there but never reached (<c>{{ if false }}{{ site_head }}{{ end }}</c>).
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
    protected SelectFieldOptionsProvider SelectFieldOptionsProvider { get; }
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
        SelectFieldOptionsProvider selectFieldOptionsProvider,
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
        SelectFieldOptionsProvider = selectFieldOptionsProvider;
        HtmlEncoder = htmlEncoder;
    }

    /// <summary>The page's HTML.</summary>
    /// <param name="templateName">The name the page's template was looked up by (<c>Page.Template</c>), for messages.</param>
    /// <param name="renderSet">The published template, which must be there, and the published layout, if any.</param>
    /// <param name="viewModel">The resolved page.</param>
    /// <param name="actionContext">The request being answered.</param>
    /// <exception cref="TimeoutException">The render took longer than <see cref="SiteScribanOptions.RenderTimeout"/>.</exception>
    /// <exception cref="BusinessException"><see cref="SiteErrorCodes.SiteTemplateDidNotRenderSiteHead"/>.</exception>
    /// <exception cref="ScriptRuntimeException">The template failed - its message carries the template name, line and column.</exception>
    public virtual async Task<string> RenderAsync(
        string templateName,
        SiteTemplateRenderSetDto renderSet,
        SiteRenderViewModel viewModel,
        ActionContext actionContext)
    {
        var template = renderSet.Template
                       ?? throw new ArgumentException("The render set has no template to render.", nameof(renderSet));

        var options = Options.Value;
        var requestAborted = actionContext.HttpContext.RequestAborted;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        timeout.CancelAfter(options.RenderTimeout);

        var scope = new ScribanRenderScope(viewModel, actionContext, renderSet);
        var globals = await ModelBuilder.BuildGlobalsAsync(viewModel);
        RegisterFunctions(globals, scope);
        var templateLoader = CreateTemplateLoader(renderSet);

        var html = await RenderTemplateAsync(templateName, template, globals, templateLoader, timeout.Token, requestAborted);
        var documentName = templateName;

        if (renderSet.Layout != null)
        {
            // The page template's output is already HTML - encoded where it had to be - so the layout writes it
            // as is.
            globals.SetValue(SiteTemplateConsts.BodyVariableName, new ScribanRawHtml(html), true);
            html = await RenderTemplateAsync(
                SiteTemplateConsts.LayoutName, renderSet.Layout, globals, templateLoader, timeout.Token, requestAborted);
            documentName = SiteTemplateConsts.LayoutName;
        }

        if (!scope.SiteHeadCalled)
        {
            throw new BusinessException(SiteErrorCodes.SiteTemplateDidNotRenderSiteHead)
                .WithData("Name", documentName);
        }

        return html;
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
        globals.SetValue(RenderFieldFunction.Name, new RenderFieldFunction(scope, PartialRenderer), true);
        globals.SetValue(SelectOptionsFunction.Name, new SelectOptionsFunction(SelectFieldOptionsProvider), true);
        globals.SetValue(HtmlRawFunction.Name, new HtmlRawFunction(), true);
    }

    /// <summary>
    /// What <c>include</c> may pull in during this render: the render set's published partials, already read with
    /// the page's template - an include costs no query.
    /// </summary>
    protected virtual ITemplateLoader CreateTemplateLoader(SiteTemplateRenderSetDto renderSet)
    {
        return new SiteTemplateLoader(renderSet.Partials, TemplateCache);
    }

    protected virtual async Task<string> RenderTemplateAsync(
        string name,
        SiteTemplateVersionDto version,
        ScriptObject globals,
        ITemplateLoader templateLoader,
        CancellationToken cancellationToken,
        CancellationToken requestAborted)
    {
        var template = TemplateCache.GetOrAdd(version, name);
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
