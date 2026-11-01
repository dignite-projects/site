using System.Threading.Tasks;
using Dignite.Site.Public.Assets;
using Dignite.Site.Themes;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;
using global::Scriban.Syntax;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Dignite.Site.Public.Scriban.Functions;

/// <summary>
/// <c>asset_url path</c>: the address of a static file of the theme - <c>asset_url "assets/site.css"</c> is
/// <c>/site-assets/assets/site.css?v=&lt;content hash&gt;</c>, under the host's base path and never with a language prefix:
/// a static file is the same in every language. The hash of the file's content makes the address change exactly when
/// the content does, so <see cref="SiteAssetController"/> can let browsers keep each address for good, and an unchanged
/// file keeps its address - and its cached copy - across releases. The hash comes with the page's theme
/// (<see cref="ScribanRenderScope.RenderSet"/>); nothing is queried here. A path the theme has no static file at fails the
/// render: a stylesheet link pointing nowhere is a broken page.
/// </summary>
public class AssetUrlFunction : SiteScriptFunction
{
    public const string Name = SiteThemeConsts.AssetUrlFunctionName;

    protected ScribanRenderScope Scope { get; }
    protected IUrlHelperFactory UrlHelperFactory { get; }

    public AssetUrlFunction(ScribanRenderScope scope, IUrlHelperFactory urlHelperFactory)
        : base(Required<string>("path"))
    {
        Scope = scope;
        UrlHelperFactory = urlHelperFactory;
    }

    protected override ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments)
    {
        var path = SiteThemePaths.Normalize(arguments[0] as string ?? string.Empty);

        var file = Scope.RenderSet.FindFile(path)
                   ?? throw new ScriptRuntimeException(span, $"No file '{path}' in the theme for {Name}.");

        if (!SiteThemePaths.IsAsset(path))
        {
            throw new ScriptRuntimeException(
                span,
                $"{Name} takes a static file - a name with an extension, such as \"assets/site.css\"; '{path}' is a template.");
        }

        var url = UrlHelperFactory.GetUrlHelper(Scope.ActionContext)
            .Content("~/" + SiteAssetUrl.RoutePrefix + "/" + path);

        return new ValueTask<object?>(url + "?" + SiteAssetUrl.VersionQueryKey + "=" + file.ContentHash);
    }
}
