using System.Threading.Tasks;
using Dignite.Site.Public.Assets;
using Dignite.Site.Templates;
using global::Scriban;
using global::Scriban.Parsing;
using global::Scriban.Runtime;
using global::Scriban.Syntax;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Dignite.Site.Public.Scriban.Functions;

/// <summary>
/// <c>asset_url name</c>: the address of a published asset template - <c>asset_url "assets/site.css"</c> is
/// <c>/site-assets/site.css?v=0123456789abcdef0123456789abcdef</c>, where <c>v</c> is the id of the published
/// version, under the host's base path and never with a language prefix: an asset is the same file in every
/// language. The id makes the address change on every publish, so <see cref="SiteAssetController"/> can let
/// browsers keep each address for good. The id and not the version number, because numbers restart at 1 when a
/// template is deleted and created again under the same name, and an address kept for a year must never be
/// reused for other bytes. The id comes with the page's templates (<see cref="ScribanRenderScope.RenderSet"/>);
/// nothing is queried here. A name that is not an asset's, or no published asset of that name, fails the render: a stylesheet link
/// pointing nowhere is a broken page.
/// </summary>
public class AssetUrlFunction : SiteScriptFunction
{
    public const string Name = "asset_url";

    protected ScribanRenderScope Scope { get; }
    protected IUrlHelperFactory UrlHelperFactory { get; }

    public AssetUrlFunction(ScribanRenderScope scope, IUrlHelperFactory urlHelperFactory)
        : base(Required<string>("name"))
    {
        Scope = scope;
        UrlHelperFactory = urlHelperFactory;
    }

    protected override ValueTask<object?> InvokeAsync(TemplateContext context, SourceSpan span, ScriptArray arguments)
    {
        var name = SiteTemplateNames.Normalize(arguments[0] as string ?? string.Empty);

        if (!SiteTemplateNames.IsAsset(name))
        {
            throw new ScriptRuntimeException(
                span,
                $"{Name} takes a name under '{SiteTemplateConsts.AssetsPrefix}', such as \"assets/site.css\"; '{name}' is not one.");
        }

        if (!Scope.RenderSet.Assets.TryGetValue(name, out var versionId))
        {
            throw new ScriptRuntimeException(span, $"No published template named '{name}' for {Name}.");
        }

        var path = UrlHelperFactory.GetUrlHelper(Scope.ActionContext)
            .Content("~/" + SiteAssetUrl.RoutePrefix + "/" + SiteTemplateNames.GetAssetPath(name));

        return new ValueTask<object?>(
            path + "?" + SiteAssetUrl.VersionQueryKey + "=" + versionId.ToString("N"));
    }
}
