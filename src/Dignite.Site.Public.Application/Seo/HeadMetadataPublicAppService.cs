using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Site.Routing;
using Dignite.Site.Seo;
using SeoTags;

namespace Dignite.Site.Public.Seo;

/// <summary>
/// The public <c>&lt;head&gt;</c>-metadata contract (总体设计 §5.3, §5.5, §5.9 - GitHub issues #13, #16,
/// #17): resolves a route, then feeds <see cref="HeadMetadataBuilder"/>'s output into <c>SeoTags</c> for
/// meta/OG/Twitter tag decisions, flattening the result into a plain <see cref="HeadMetadataDto"/> with no
/// third-party type in it. JSON-LD structured data is left to whichever frontend renders the page - it has
/// direct access to the same content and field data this service reads (总体设计 §5.4 decision), and can
/// choose its own schema.org mapping rather than being bound to the one this backend used to hard-code.
/// </summary>
public class HeadMetadataPublicAppService : PublicAppService, IHeadMetadataPublicAppService
{
    protected SiteRouteResolver RouteResolver { get; }

    protected HeadMetadataBuilder HeadMetadataBuilder { get; }

    protected SiteUrlBuilder UrlBuilder { get; }

    public HeadMetadataPublicAppService(
        SiteRouteResolver routeResolver, HeadMetadataBuilder headMetadataBuilder, SiteUrlBuilder urlBuilder)
    {
        RouteResolver = routeResolver;
        HeadMetadataBuilder = headMetadataBuilder;
        UrlBuilder = urlBuilder;
    }

    public virtual async Task<HeadMetadataDto?> ResolveAsync(ResolveHeadMetadataInput input)
    {
        // The one place a culture prefix is stripped off a raw request path - same reason and same
        // mechanism as RoutingPublicAppService.ResolveAsync.
        var urlContext = await UrlBuilder.CreateContextAsync();
        urlContext.TryStripCulturePrefix(input.Path, out var cultureName, out var remainingPath);

        // includeUnpublished stays false explicitly - the public surface never previews a draft, the same
        // rule RoutingPublicAppService's own resolve-path endpoint follows. The noindex-forcing that
        // obligation demands is discharged through BuildDtoAsync instead, for a future authenticated
        // preview caller that passes true there.
        var match = await RouteResolver.ResolveAsync(remainingPath, cultureName, includeUnpublished: false);

        return match.IsMatch ? await BuildDtoAsync(match, cultureName, includeUnpublished: false) : null;
    }

    /// <summary>
    /// Not on the public interface. A future in-process Tier 0 renderer (#21) that already holds a
    /// resolved <see cref="RouteMatch"/> can inject this concrete class and call this directly, skipping
    /// the redundant route re-resolution <see cref="ResolveAsync"/> otherwise performs - and a future
    /// authenticated preview caller is this method's entry point for <paramref name="includeUnpublished"/>
    /// <see langword="true"/>, which <see cref="HeadMetadataBuilder"/> turns into a forced <c>noindex</c>.
    /// </summary>
    public virtual async Task<HeadMetadataDto> BuildDtoAsync(
        RouteMatch match, string cultureName, bool includeUnpublished)
    {
        var metadata = await HeadMetadataBuilder.BuildAsync(match, cultureName, includeUnpublished);
        return ConvertToDto(metadata);
    }

    protected virtual HeadMetadataDto ConvertToDto(HeadMetadata metadata)
    {
        // "Feed it, don't reimplement it" (GitHub issue #13): SeoInfo decides the Twitter card type from
        // what is fed in (an image means summary_large_image, no image means summary), rather than this
        // service hand-picking either.
        var seoInfo = new SeoInfo();
        // SeoTags rejects a blank description outright (SetCommonInfo -> EnsureNotNullOrWhiteSpace), but
        // not every content has one - falling back to the title keeps this call from throwing without
        // misrepresenting the DTO, since MetaDescription below is set from metadata.Description directly
        // and this fed-in value is never read back out of seoInfo.
        seoInfo.SetCommonInfo(
            metadata.Title, metadata.Description ?? metadata.Title, metadata.CanonicalUrl, Array.Empty<string>());
        seoInfo.MetaLink.Robots = metadata.NoIndex ? "noindex" : null;

        if (metadata.OgImageUrl != null)
        {
            seoInfo.SetImageInfo(metadata.OgImageUrl);
        }

        // Nothing here ever calls SetArticleInfo/SetProducInfo - this service has no notion of what
        // schema.org shape the content represents, so OpenGraph.Type sits at SeoTags' own Website default.
        // A renderer that builds its own JSON-LD and knows the content is an Article/Product is free to
        // override og:type itself.

        return new HeadMetadataDto
        {
            MetaTitle = metadata.Title,
            MetaDescription = metadata.Description,
            CanonicalUrl = metadata.CanonicalUrl,
            RobotsContent = seoInfo.MetaLink.Robots,
            OgImageUrl = seoInfo.OpenGraph.ImageUrl,
            OgType = ToWireValue(seoInfo.OpenGraph.Type),
            TwitterCardType = ToWireValue(seoInfo.TwitterCard.CardType),
            HreflangAlternates = metadata.HreflangAlternates
                .Select(a => new HreflangAlternateDto { CultureName = a.CultureName, Url = a.Url })
                .ToList(),
            XDefaultUrl = metadata.XDefaultUrl
        };
    }

    /// <summary>
    /// The actual <c>og:type</c>/<c>twitter:card</c> attribute values SeoTags would render (e.g.
    /// <c>"article"</c>, <c>"summary_large_image"</c>). SeoTags keeps this mapping
    /// (<c>Utilities.ToDisplay&lt;TEnum&gt;</c>) <c>internal</c>, so nothing outside the library can reach
    /// it - confirmed by reflection - and the flattened <see cref="HeadMetadataDto"/> carries plain
    /// strings a renderer uses as-is, not the SeoTags enum type, so it has to be re-derived here.
    /// </summary>
    private static string? ToWireValue(OpenGraphType? type)
    {
        return type switch
        {
            null => null,
            OpenGraphType.Website => "website",
            OpenGraphType.Article => "article",
            OpenGraphType.Product => "product",
            OpenGraphType.Book => "book",
            _ => type.Value.ToString().ToLowerInvariant()
        };
    }

    private static string? ToWireValue(TwitterCardType? type)
    {
        return type switch
        {
            null => null,
            TwitterCardType.Summary => "summary",
            TwitterCardType.SummaryLargeImage => "summary_large_image",
            TwitterCardType.Player => "player",
            _ => type.Value.ToString().ToLowerInvariant()
        };
    }
}
