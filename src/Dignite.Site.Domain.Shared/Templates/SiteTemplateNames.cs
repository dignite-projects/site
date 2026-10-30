using System;
using Volo.Abp;

namespace Dignite.Site.Templates;

/// <summary>
/// What a template's name says about its kind. All templates share one name space and one storage
/// (<c>SiteTemplate</c> with its versions); a reserved first segment tells them apart:
/// <list type="bullet">
/// <item><see cref="SiteTemplateConsts.PartialsPrefix"/> (<c>partials/post-card</c>) - a fragment that a page
/// template or the layout pulls in with <c>{{ include "partials/post-card" }}</c>. Only a partial can be
/// included, and a partial is never rendered as a page.</item>
/// <item><see cref="SiteTemplateConsts.AssetsPrefix"/> (<c>assets/site.css</c>) - a text file (CSS, JS, ...)
/// served as it is by <c>GET /site-assets/{path}</c>; its body is not Scriban and is not parsed. A template
/// gets its versioned address with <c>asset_url "assets/site.css"</c>.</item>
/// <item>Everything else - a page template (<c>Default</c>, <c>Blog/Post</c>) or the <c>layout</c>.</item>
/// </list>
/// Prefixes are matched exactly and in lower case, like the rest of a name.
/// </summary>
public static class SiteTemplateNames
{
    /// <summary>
    /// The canonical stored form of a name: trimmed, without a leading slash. The same normalization
    /// <c>Page.SetTemplate</c> applies, so a page's template value and a template's name compare equal.
    /// </summary>
    public static string Normalize(string name)
    {
        Check.NotNull(name, nameof(name));

        return name.Trim().RemovePreFix("/");
    }

    /// <summary>Whether the (normalized) <paramref name="name"/> is under <see cref="SiteTemplateConsts.PartialsPrefix"/>.</summary>
    public static bool IsPartial(string name)
    {
        return HasPrefix(name, SiteTemplateConsts.PartialsPrefix);
    }

    /// <summary>Whether the (normalized) <paramref name="name"/> is under <see cref="SiteTemplateConsts.AssetsPrefix"/>.</summary>
    public static bool IsAsset(string name)
    {
        return HasPrefix(name, SiteTemplateConsts.AssetsPrefix);
    }

    /// <summary>Whether the (normalized) <paramref name="name"/> can render a page: neither a partial nor an asset.</summary>
    public static bool IsPage(string name)
    {
        return !IsPartial(name) && !IsAsset(name);
    }

    /// <summary>
    /// The part of an asset's name after <see cref="SiteTemplateConsts.AssetsPrefix"/> - what follows
    /// <c>/site-assets/</c> in its address: <c>site.css</c> for <c>assets/site.css</c>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not an asset name.</exception>
    public static string GetAssetPath(string name)
    {
        if (!IsAsset(name))
        {
            throw new ArgumentException($"'{name}' is not under '{SiteTemplateConsts.AssetsPrefix}'.", nameof(name));
        }

        return name.Substring(SiteTemplateConsts.AssetsPrefix.Length);
    }

    /// <summary>The asset name of a path under <c>/site-assets/</c>: <c>assets/site.css</c> for <c>site.css</c>.</summary>
    public static string GetAssetName(string assetPath)
    {
        return SiteTemplateConsts.AssetsPrefix + Normalize(assetPath);
    }

    private static bool HasPrefix(string name, string prefix)
    {
        return name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.Ordinal);
    }
}
