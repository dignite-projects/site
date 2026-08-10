using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Site.Common;
using Dignite.Site.Pages;
using Dignite.Site.Routing;
using Dignite.Site.Seo;

namespace Dignite.Site.Public.Routing;

public class RoutingPublicAppService : PublicAppService, IRoutingPublicAppService
{
    protected SiteRouteResolver RouteResolver { get; }

    protected SiteUrlBuilder UrlBuilder { get; }

    public RoutingPublicAppService(SiteRouteResolver routeResolver, SiteUrlBuilder urlBuilder)
    {
        RouteResolver = routeResolver;
        UrlBuilder = urlBuilder;
    }

    public virtual async Task<RouteMatchDto> ResolveAsync(ResolvePathInput input)
    {
        // The one place a culture prefix is stripped off a raw request path - Public.Web can't do it
        // itself (SiteUrlContext is Domain-only), so it happens here, server-side, before routing.
        var urlContext = await UrlBuilder.CreateContextAsync();
        urlContext.TryStripCulturePrefix(input.Path, out var cultureName, out var remainingPath);

        // includeUnpublished stays false explicitly - the public surface never previews a draft, and
        // SiteRouteResolver's own doc warns that a caller passing true must force noindex on the response,
        // which nothing here does.
        var match = await RouteResolver.ResolveAsync(remainingPath, cultureName, includeUnpublished: false);

        var contentDto = match.Content?.ToDto(ObjectMapper);
        if (contentDto != null && match.Page != null)
        {
            contentDto.Url = UrlBuilder.BuildContentPath(urlContext, match.Page, match.Content!);
        }

        return new RouteMatchDto
        {
            Matched = match.IsMatch,
            Kind = MapKind(match.Kind),
            CultureName = cultureName,
            Page = match.Page == null ? null : ObjectMapper.Map<Page, PageDto>(match.Page),
            Content = contentDto,
            ContentType = match.ContentType?.ToDto(ObjectMapper),
            FilterValues = new Dictionary<string, string>(match.FilterValues, StringComparer.OrdinalIgnoreCase)
        };
    }

    protected static RouteMatchKindDto MapKind(RouteMatchKind kind)
    {
        return kind switch
        {
            RouteMatchKind.None => RouteMatchKindDto.None,
            RouteMatchKind.Page => RouteMatchKindDto.Page,
            RouteMatchKind.ContentOfPage => RouteMatchKindDto.ContentOfPage,
            RouteMatchKind.Content => RouteMatchKindDto.Content,
            _ => RouteMatchKindDto.None
        };
    }
}
