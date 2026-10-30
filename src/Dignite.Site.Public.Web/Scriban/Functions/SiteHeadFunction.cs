using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Dignite.Site.Public.TagHelpers;
using Dignite.Site.Templates;
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
/// </summary>
public class SiteHeadFunction : SiteScriptFunction
{
    public const string Name = SiteTemplateConsts.SiteHeadFunctionName;

    protected ScribanRenderScope Scope { get; }
    protected HtmlEncoder Encoder { get; }

    public SiteHeadFunction(ScribanRenderScope scope, HtmlEncoder encoder)
        : base(Optional<string>("title"), Optional<string>("description"))
    {
        Scope = scope;
        Encoder = encoder;
    }

    protected override ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments)
    {
        Scope.SiteHeadCalled = true;

        return new ValueTask<object?>(new ScribanRawHtml(SiteHeadMetadataHtml.Build(
            Scope.ViewModel.HeadMetadata,
            OptionalString(arguments, 0),
            OptionalString(arguments, 1),
            currentPage: 1,
            pageQueryKey: "page",
            Encoder)));
    }
}
