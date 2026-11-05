using System;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Dignite.Site.Public.Localization;
using Dignite.Site.Public.Routing;
using Dignite.Site.Public.Seo;
using Dignite.Site.Public.TagHelpers;
using Dignite.Site.Themes;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;
using global::Scriban.Syntax;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dignite.Site.Public.Scriban.Functions;

/// <summary>
/// <c>site_head [title] [description]</c>, or <c>site_head { title:, description:, image:, type:, schema: }</c>: the
/// page's SEO head tags - description, canonical, robots, Open Graph, Twitter card, hreflang, x-default and the
/// structured data the template gives - exactly what <c>&lt;site-head-metadata&gt;</c> writes in a Razor template
/// (<see cref="SiteHeadMetadataHtml"/>). Every render has to call it - see <see cref="ScribanRenderScope.SiteHeadCalled"/>.
/// Returned as <see cref="ScribanRawHtml"/>, so it is written unencoded.
/// <list type="bullet">
/// <item><c>title</c> overrides <c>og:title</c>; <c>description</c> is used only when the content has no description
/// of its own.</item>
/// <item><c>image</c> is a share image for the page when its SEO field has none: an address, or a file field's value
/// (its first file). It takes the place of the site's default share image.</item>
/// <item><c>type</c> is <c>og:type</c>, <c>website</c> or <c>article</c>; <c>article</c> also writes the content's
/// publish and modification times.</item>
/// <item><c>schema</c> is a schema.org node or a list of them, written by <see cref="SiteJsonLd"/>.</item>
/// </list>
/// <para>
/// Beyond page 1 of a paged list (<see cref="SiteRenderViewModel.CurrentPage"/>, the request's page query
/// parameter) every address written - canonical, og:url, each hreflang alternate and x-default - carries the page
/// number, as the tag helper's <c>current-page</c> does: page 2 lists other items than page 1, so it must not
/// canonicalise to it. The template passes nothing for that - the platform knows the page number, and a page that
/// forgot would ship the wrong canonical.
/// </para>
/// </summary>
public class SiteHeadFunction : SiteScriptFunction
{
    public const string Name = SiteThemeConsts.SiteHeadFunctionName;

    /// <summary>The keys of the options object.</summary>
    public static readonly string[] OptionNames = { "title", "description", "image", "type", "schema" };

    protected ScribanRenderScope Scope { get; }
    protected HtmlEncoder Encoder { get; }

    /// <summary><see cref="SiteScribanOptions.PageQueryKey"/>.</summary>
    protected string PageQueryKey { get; }

    public SiteHeadFunction(ScribanRenderScope scope, HtmlEncoder encoder, string pageQueryKey)
        : base(Optional<object>("title"), Optional<string>("description"))
    {
        Scope = scope;
        Encoder = encoder;
        PageQueryKey = pageQueryKey;
    }

    protected override ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments)
    {
        Scope.SiteHeadCalled = true;

        var options = arguments[0] is ScriptObject optionsObject
            ? ReadOptions(context, span, optionsObject, arguments)
            : new SiteHeadOptions
            {
                Title = arguments[0] == null ? null : NullIfEmpty(context.ObjectToString(arguments[0])),
                FallbackDescription = OptionalString(arguments, 1)
            };

        var content = Scope.ViewModel.Content?.Content;
        if (content != null)
        {
            options.PublishedTime = content.PublishTime;
            options.ModifiedTime = content.LastModificationTime;
        }

        return new ValueTask<object?>(new ScribanRawHtml(SiteHeadMetadataHtml.Build(
            Scope.ViewModel.HeadMetadata,
            options,
            Scope.ViewModel.CurrentPage,
            PageQueryKey,
            Encoder)));
    }

    protected virtual SiteHeadOptions ReadOptions(TemplateContext context, SourceSpan span, ScriptObject options, ScriptArray arguments)
    {
        if (arguments.Count > 1 && arguments[1] != null)
        {
            throw new ScriptRuntimeException(span, "site_head takes either an object of options or a title and a description, not both.");
        }

        if (options.GetMembers().FirstOrDefault(m => !OptionNames.Contains(m)) is { } unknown)
        {
            throw new ScriptRuntimeException(span, $"site_head has no option `{unknown}`; the options are {string.Join(", ", OptionNames)}.");
        }

        var type = GetOptionString(context, span, options, "type");
        if (type != null && !SiteHeadOptions.OgTypes.Contains(type))
        {
            throw new ScriptRuntimeException(span, $"site_head's type is `{type}`; it can be {string.Join(" or ", SiteHeadOptions.OgTypes)}.");
        }

        var result = new SiteHeadOptions
        {
            Title = GetOptionString(context, span, options, "title"),
            FallbackDescription = GetOptionString(context, span, options, "description"),
            Image = ReadImage(context, span, options),
            OgType = type
        };

        if (options.TryGetValue(context, span, "schema", out var schema) && schema != null)
        {
            try
            {
                result.JsonLd = SiteJsonLd.Serialize(ScribanValues.ToJsonNode(context, span, schema), CreateUrlResolver());
            }
            catch (ArgumentException ex)
            {
                throw new ScriptRuntimeException(span, $"site_head's schema: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>
    /// <c>image</c>: an address, or a file field's value - a list of file objects, of which the first is taken - or one
    /// file object, whose <c>url</c> is the address.
    /// </summary>
    protected virtual string? ReadImage(TemplateContext context, SourceSpan span, ScriptObject options)
    {
        if (!options.TryGetValue(context, span, "image", out var image) || image == null)
        {
            return null;
        }

        if (image is ScriptArray files)
        {
            image = files.Count == 0 ? null : files[0];
        }

        return image switch
        {
            null => null,
            ScriptObject file => file.TryGetValue(context, span, "url", out var url) && url != null
                ? NullIfEmpty(context.ObjectToString(url))
                : null,
            _ => NullIfEmpty(context.ObjectToString(image))
        };
    }

    /// <summary>
    /// How the structured data's addresses are made absolute: <c>~/...</c> in the page's language, as <c>url</c> writes
    /// it, then on the site's primary domain (<see cref="SiteJsonLd.CreateUrlResolver"/>).
    /// </summary>
    protected virtual Func<string, string> CreateUrlResolver()
    {
        var services = Scope.ActionContext.HttpContext.RequestServices;
        var languages = services.GetRequiredService<SiteLanguageContext>().GetLoadedLanguages();
        var excludedPathPrefixes = services.GetRequiredService<IOptions<SiteLanguageOptions>>().Value.ExcludedPathPrefixes;
        var cultureName = Scope.ViewModel.CultureName;

        return SiteJsonLd.CreateUrlResolver(
            Scope.ViewModel.HeadMetadata?.BaseUrl,
            path => SiteLinks.Localize(path, cultureName, languages, excludedPathPrefixes));
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
