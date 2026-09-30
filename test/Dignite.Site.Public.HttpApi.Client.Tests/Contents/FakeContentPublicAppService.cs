using System;
using System.Threading.Tasks;
using Dignite.Site.Contents;
using Volo.Abp.Application.Dtos;

namespace Dignite.Site.Public.Contents;

/// <summary>
/// Records what the controller bound from the route and query string, so
/// <see cref="ContentListHttpIntegrationTests"/> and <see cref="ContentAdjacentHttpIntegrationTests"/> can
/// compare it with what the client proxy was given - see <see cref="ContentHttpServerTestModule"/>, which
/// registers it as a singleton for exactly that reason.
/// </summary>
public class FakeContentPublicAppService : IContentPublicAppService
{
    public GetContentListInput? LastListInput { get; private set; }

    public Guid? LastAdjacentId { get; private set; }

    public GetAdjacentContentsInput? LastAdjacentInput { get; private set; }

    /// <summary>What <see cref="GetAdjacentAsync"/> answers with - set by the test before calling.</summary>
    public AdjacentContentsDto AdjacentResult { get; set; } = new();

    public Task<AdjacentContentsDto> GetAdjacentAsync(Guid id, GetAdjacentContentsInput input)
    {
        LastAdjacentId = id;
        LastAdjacentInput = input;
        return Task.FromResult(AdjacentResult);
    }

    public Task<PagedResultDto<ContentDto>> GetListAsync(GetContentListInput input)
    {
        LastListInput = input;
        return Task.FromResult(new PagedResultDto<ContentDto>());
    }

    public Task<ContentDto> GetAsync(Guid id)
    {
        throw new NotImplementedException();
    }

    public Task<ContentDto> GetBySlugAsync(Guid pageId, string cultureName, string slug)
    {
        throw new NotImplementedException();
    }

    public Task<ListResultDto<ContentDto>> GetTranslationsAsync(Guid pageId, Guid contentTypeId, string slug)
    {
        throw new NotImplementedException();
    }
}
