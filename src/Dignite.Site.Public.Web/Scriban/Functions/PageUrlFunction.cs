using System.Threading.Tasks;
using Dignite.Site.Public.Localization;
using Dignite.Site.Public.TagHelpers;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Dignite.Site.Public.Scriban.Functions;

/// <summary>
/// <c>page_url page_no</c>: the address of a page of the paged list the current page is - the link a pager writes.
/// The current path, language prefix and all, under the host's base path, with the page number under
/// <see cref="SiteScribanOptions.PageQueryKey"/> beyond page 1 (<c>/ja/blog?page=3</c>) and without it for page 1
/// (<c>/ja/blog</c>), which is the address <c>site_head</c> writes as canonical. The current path is the one being
/// rendered: a preview's site path (<see cref="SiteLanguageContext.RenderedPath"/>), not the API request that asked
/// for it. Nothing else of the request's query string is kept: a template does not see it, and the page's own
/// address is what the route gave it.
/// </summary>
public class PageUrlFunction : SiteScriptFunction
{
    public const string Name = "page_url";

    protected ScribanRenderScope Scope { get; }
    protected SiteLanguageContext LanguageContext { get; }
    protected IUrlHelperFactory UrlHelperFactory { get; }

    /// <summary><see cref="SiteScribanOptions.PageQueryKey"/>.</summary>
    protected string PageQueryKey { get; }

    public PageUrlFunction(
        ScribanRenderScope scope,
        SiteLanguageContext languageContext,
        IUrlHelperFactory urlHelperFactory,
        string pageQueryKey)
        : base(Required<int>("page_no"))
    {
        Scope = scope;
        LanguageContext = languageContext;
        UrlHelperFactory = urlHelperFactory;
        PageQueryKey = pageQueryKey;
    }

    protected override ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments)
    {
        var pageNo = (int)arguments[0]!;
        var path = LanguageContext.RenderedPath ?? Scope.ActionContext.HttpContext.Request.Path.Value;
        if (string.IsNullOrEmpty(path))
        {
            path = "/";
        }

        var url = UrlHelperFactory.GetUrlHelper(Scope.ActionContext).Content("~" + path);
        return new ValueTask<object?>(SiteHeadMetadataHtml.Paged(url, pageNo, PageQueryKey));
    }
}
