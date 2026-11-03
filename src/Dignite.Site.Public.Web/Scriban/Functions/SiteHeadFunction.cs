using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Dignite.Site.Public.Routing;
using Dignite.Site.Public.TagHelpers;
using Dignite.Site.Themes;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;

namespace Dignite.Site.Public.Scriban.Functions;

/// <summary>
/// <c>site_head [title] [description]</c>: the page's SEO head tags - description, canonical, robots, Open
/// Graph, Twitter card, hreflang, x-default - exactly what <c>&lt;site-head-metadata&gt;</c> writes in a Razor
/// template (<see cref="SiteHeadMetadataHtml"/>). <c>title</c> overrides <c>og:title</c>; <c>description</c> is
/// used only when the content has no description of its own. Every render has to call it - see
/// <see cref="ScribanRenderScope.SiteHeadCalled"/>. Returned as <see cref="ScribanRawHtml"/>, so it is written unencoded.
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

    protected ScribanRenderScope Scope { get; }
    protected HtmlEncoder Encoder { get; }

    /// <summary><see cref="SiteScribanOptions.PageQueryKey"/>.</summary>
    protected string PageQueryKey { get; }

    public SiteHeadFunction(ScribanRenderScope scope, HtmlEncoder encoder, string pageQueryKey)
        : base(Optional<string>("title"), Optional<string>("description"))
    {
        Scope = scope;
        Encoder = encoder;
        PageQueryKey = pageQueryKey;
    }

    protected override ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments)
    {
        Scope.SiteHeadCalled = true;

        return new ValueTask<object?>(new ScribanRawHtml(SiteHeadMetadataHtml.Build(
            Scope.ViewModel.HeadMetadata,
            OptionalString(arguments, 0),
            OptionalString(arguments, 1),
            Scope.ViewModel.CurrentPage,
            PageQueryKey,
            Encoder)));
    }
}
