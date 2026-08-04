using System.Threading.Tasks;
using Dignite.Sites.Common;
using Dignite.Sites.Contents;
using Dignite.Sites.ContentTypes;
using Dignite.Sites.Pages;
using Dignite.Sites.Routing;

namespace Dignite.Sites.Public.Routing;

public class RoutingPublicAppService : PublicAppService, IRoutingPublicAppService
{
    protected SiteRouteResolver RouteResolver { get; }

    public RoutingPublicAppService(SiteRouteResolver routeResolver)
    {
        RouteResolver = routeResolver;
    }

    public virtual async Task<RouteMatchDto> ResolveAsync(ResolvePathInput input)
    {
        // includeUnpublished stays false explicitly - the public surface never previews a draft, and
        // SiteRouteResolver's own doc warns that a caller passing true must force noindex on the response,
        // which nothing here does.
        var match = await RouteResolver.ResolveAsync(input.Path, input.CultureName, includeUnpublished: false);

        return new RouteMatchDto
        {
            Matched = match.IsMatch,
            Kind = MapKind(match.Kind),
            Page = match.Page == null ? null : ObjectMapper.Map<Page, PageDto>(match.Page),
            Content = match.Content == null ? null : MapContent(match.Content),
            ContentType = match.ContentType == null ? null : MapContentType(match.ContentType)
        };
    }

    protected virtual ContentDto MapContent(Content content)
    {
        var dto = ObjectMapper.Map<Content, ContentDto>(content);
        dto.FieldValues = content.FlexFields.ToValueDictionary();
        return dto;
    }

    protected virtual ContentTypeDto MapContentType(ContentType contentType)
    {
        var dto = ObjectMapper.Map<ContentType, ContentTypeDto>(contentType);
        dto.Fields = contentType.Fields.ToDtoList();
        return dto;
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
